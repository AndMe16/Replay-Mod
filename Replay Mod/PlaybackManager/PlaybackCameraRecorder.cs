using BepInEx;
using Replay_Mod;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace ReplayMod.PlaybackManager
{
    public class PlaybackCameraRecorder : MonoBehaviour
    {
        private enum VideoExportMode
        {
            FfmpegPipeMp4,
            RawRgbSequence
        }

        [Header("References")]
        public LEV_MoveCamera cam;
        public Camera mainCamera;
        public Camera skyCamera;

        [Header("Recording")]
        public int width = 1920;
        public int height = 1080;
        public int targetFPS = 60;
        public string outputFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Zeepkist",
            "Mods",
            "Replay Mod",
            "Recordings");
        public string outputFileName = "playback_capture.mp4";
        public string ffmpegPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "ffmpeg", "ffmpeg.exe");

        [Header("Performance")]
        [SerializeField]
        private VideoExportMode exportMode = VideoExportMode.FfmpegPipeMp4;

        [SerializeField]
        [Range(1, 16)]
        private int maxInFlightReadbacks = 4;

        [SerializeField]
        [Range(4, 256)]
        private int maxFrameQueue = 64;

        [SerializeField]
        private bool dropFramesWhenBusy = true;

        public bool recording;
        private int frame;
        private int droppedFrames;
        private int enqueuedFrames;
        private int writtenFrames;

        public TimeSpan recordingTime = TimeSpan.Zero;
        private float recordingStartRealtime;

        private RenderTexture renderTexture;
        private Coroutine captureCoroutine;
        private Coroutine finalizeCoroutine;

        private readonly ConcurrentQueue<FramePacket> frameQueue = new ConcurrentQueue<FramePacket>();
        private readonly ConcurrentQueue<byte[]> reusableBuffers = new ConcurrentQueue<byte[]>();

        private Thread writerThread;
        private volatile bool writerRunning;
        private volatile string writerError;
        private ManualResetEvent writerSignal;

        private int pendingReadbacks;
        private int frameByteCount;
        private string resolvedOutputFolder;
        private string resolvedVideoPath;

        private FfmpegPipeWriter ffmpegWriter;

        private struct FramePacket
        {
            public int FrameIndex;
            public byte[] Data;
        }

        private void Update()
        {
            if (recording)
            {
                recordingTime = TimeSpan.FromSeconds(Time.realtimeSinceStartup - recordingStartRealtime);
            }
        }

        public void StartRecording()
        {
            if (recording)
            {
                return;
            }

            if (mainCamera == null)
            {
                Plugin.logger.LogError("[PlaybackCameraRecorder] mainCamera is null, aborting recording start.");
                return;
            }

            frame = 0;
            droppedFrames = 0;
            enqueuedFrames = 0;
            writtenFrames = 0;
            pendingReadbacks = 0;
            writerError = null;
            frameByteCount = width * height * 3;

            resolvedOutputFolder = ResolveOutputFolder();
            Directory.CreateDirectory(resolvedOutputFolder);
            resolvedVideoPath = Path.Combine(resolvedOutputFolder, outputFileName);

            renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
            {
                antiAliasing = 1,
                useMipMap = false,
                autoGenerateMips = false
            };
            renderTexture.Create();

            writerSignal = new ManualResetEvent(false);

            if (!TryStartWriter())
            {
                CleanupRuntimeState();
                return;
            }

            recording = true;
            Time.captureFramerate = targetFPS;

            recordingTime = TimeSpan.Zero;
            recordingStartRealtime = Time.realtimeSinceStartup;

            captureCoroutine = StartCoroutine(CaptureLoop());

            Plugin.logger.LogInfo($"[PlaybackCameraRecorder] Recording started ({width}x{height}@{targetFPS}) => {resolvedVideoPath}");
        }

        public void StopRecording()
        {
            if (!recording)
            {
                return;
            }

            recording = false;
            Time.captureFramerate = 0;

            if (captureCoroutine != null)
            {
                StopCoroutine(captureCoroutine);
                captureCoroutine = null;
            }

            if (finalizeCoroutine == null)
            {
                finalizeCoroutine = StartCoroutine(FinalizeRecording());
            }
        }

        private IEnumerator CaptureLoop()
        {
            while (recording)
            {
                yield return new WaitForEndOfFrame();

                RenderToTarget();

                if (Volatile.Read(ref pendingReadbacks) >= maxInFlightReadbacks)
                {
                    droppedFrames++;
                    continue;
                }

                if (frameQueue.Count >= maxFrameQueue)
                {
                    if (dropFramesWhenBusy)
                    {
                        droppedFrames++;
                        continue;
                    }

                    while (frameQueue.Count >= maxFrameQueue && recording)
                    {
                        yield return null;
                    }
                }

                Interlocked.Increment(ref pendingReadbacks);
                int frameIndex = frame++;
                AsyncGPUReadback.Request(renderTexture, 0, TextureFormat.RGB24, request => OnReadbackComplete(request, frameIndex));
            }
        }

        private void RenderToTarget()
        {
            if (skyCamera != null)
            {
                skyCamera.targetTexture = renderTexture;
                skyCamera.Render();
                skyCamera.targetTexture = null;
            }

            mainCamera.targetTexture = renderTexture;
            mainCamera.Render();
            mainCamera.targetTexture = null;
        }

        private void OnReadbackComplete(AsyncGPUReadbackRequest request, int frameIndex)
        {
            try
            {
                if (request.hasError)
                {
                    droppedFrames++;
                    return;
                }

                if (!writerRunning)
                {
                    droppedFrames++;
                    return;
                }

                NativeArray<byte> source = request.GetData<byte>();
                byte[] buffer = RentBuffer();
                source.CopyTo(buffer);

                frameQueue.Enqueue(new FramePacket
                {
                    FrameIndex = frameIndex,
                    Data = buffer
                });

                enqueuedFrames++;
                writerSignal.Set();
            }
            catch (Exception ex)
            {
                writerError = $"Readback callback failed: {ex}";
                Plugin.logger.LogError($"[PlaybackCameraRecorder] {writerError}");
            }
            finally
            {
                Interlocked.Decrement(ref pendingReadbacks);
            }
        }

        private IEnumerator FinalizeRecording()
        {
            const float timeoutSeconds = 20f;
            float started = Time.realtimeSinceStartup;

            while ((Volatile.Read(ref pendingReadbacks) > 0 || !frameQueue.IsEmpty) && Time.realtimeSinceStartup - started < timeoutSeconds)
            {
                yield return null;
            }

            writerRunning = false;
            writerSignal?.Set();

            if (writerThread != null && writerThread.IsAlive)
            {
                writerThread.Join(5000);
            }

            CleanupRuntimeState();

            Plugin.logger.LogInfo($"[PlaybackCameraRecorder] Recording stopped. enqueued={enqueuedFrames}, written={writtenFrames}, dropped={droppedFrames}");

            if (!string.IsNullOrEmpty(writerError))
            {
                Plugin.logger.LogError($"[PlaybackCameraRecorder] Writer error: {writerError}");
            }

            finalizeCoroutine = null;
        }

        private bool TryStartWriter()
        {
            writerRunning = true;

            if (exportMode == VideoExportMode.FfmpegPipeMp4)
            {
                ffmpegWriter = new FfmpegPipeWriter(ffmpegPath, width, height, targetFPS, resolvedVideoPath);
                if (!ffmpegWriter.Start())
                {
                    writerError = ffmpegWriter.StartError;
                    Plugin.logger.LogError($"[PlaybackCameraRecorder] Failed to start ffmpeg writer: {writerError}");
                    writerRunning = false;
                    return false;
                }
            }

            writerThread = new Thread(WriterLoop)
            {
                IsBackground = true,
                Name = "PlaybackRecorderWriter"
            };
            writerThread.Start();

            return true;
        }

        private void WriterLoop()
        {
            try
            {
                while (writerRunning || !frameQueue.IsEmpty)
                {
                    if (!frameQueue.TryDequeue(out FramePacket packet))
                    {
                        writerSignal.WaitOne(10);
                        continue;
                    }

                    if (exportMode == VideoExportMode.FfmpegPipeMp4)
                    {
                        ffmpegWriter.WriteFrame(packet.Data, frameByteCount);
                    }
                    else
                    {
                        string file = Path.Combine(resolvedOutputFolder, $"frame_{packet.FrameIndex:D05}.rgb");
                        File.WriteAllBytes(file, packet.Data);
                    }

                    ReturnBuffer(packet.Data);
                    Interlocked.Increment(ref writtenFrames);
                }
            }
            catch (Exception ex)
            {
                writerError = ex.ToString();
                Plugin.logger.LogError($"[PlaybackCameraRecorder] Writer loop failed: {writerError}");
            }
            finally
            {
                try
                {
                    ffmpegWriter?.Dispose();
                }
                catch (Exception ex)
                {
                    Plugin.logger.LogError($"[PlaybackCameraRecorder] ffmpeg dispose failed: {ex}");
                }

                ffmpegWriter = null;
            }
        }

        private byte[] RentBuffer()
        {
            if (reusableBuffers.TryDequeue(out byte[] existing) && existing != null && existing.Length == frameByteCount)
            {
                return existing;
            }

            return new byte[frameByteCount];
        }

        private void ReturnBuffer(byte[] buffer)
        {
            if (buffer == null || buffer.Length != frameByteCount)
            {
                return;
            }

            if (reusableBuffers.Count >= maxFrameQueue * 2)
            {
                return;
            }

            reusableBuffers.Enqueue(buffer);
        }

        private string ResolveOutputFolder()
        {
            if (Path.IsPathRooted(outputFolder))
            {
                return outputFolder;
            }

            return Path.Combine(Application.persistentDataPath, outputFolder);
        }

        private void CleanupRuntimeState()
        {
            if (renderTexture != null)
            {
                renderTexture.Release();
                Destroy(renderTexture);
                renderTexture = null;
            }

            writerSignal?.Dispose();
            writerSignal = null;

            while (frameQueue.TryDequeue(out FramePacket packet))
            {
                ReturnBuffer(packet.Data);
            }

            writerThread = null;
            writerRunning = false;
        }

        private void OnDisable()
        {
            if (recording)
            {
                StopRecording();
            }
        }

        private sealed class FfmpegPipeWriter : IDisposable
        {
            private readonly string ffmpegPath;
            private readonly int width;
            private readonly int height;
            private readonly int fps;
            private readonly string outputPath;
            private Process process;
            private Stream stdin;

            public string StartError { get; private set; }

            public FfmpegPipeWriter(string ffmpegPath, int width, int height, int fps, string outputPath)
            {
                this.ffmpegPath = ffmpegPath;
                this.width = width;
                this.height = height;
                this.fps = fps;
                this.outputPath = outputPath;
            }

            public bool Start()
            {
                try
                {
                    string args =
                        $"-y -f rawvideo -pix_fmt rgb24 -s {width}x{height} -r {fps} -i - " +
                        "-vf vflip " +
                        "-an -c:v libx264 -preset fast -crf 18 -pix_fmt yuv420p " +
                        $"\"{outputPath}\"";

                    process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = ffmpegPath,
                            Arguments = args,
                            UseShellExecute = false,
                            RedirectStandardInput = true,
                            RedirectStandardError = true,
                            CreateNoWindow = true
                        }
                    };

                    bool started = process.Start();
                    if (!started)
                    {
                        StartError = "ffmpeg process did not start.";
                        return false;
                    }

                    stdin = process.StandardInput.BaseStream;
                    return true;
                }
                catch (Exception ex)
                {
                    StartError = ex.ToString();
                    return false;
                }
            }

            public void WriteFrame(byte[] frameData, int byteCount)
            {
                if (stdin == null)
                {
                    throw new InvalidOperationException("ffmpeg stdin stream is not available.");
                }

                stdin.Write(frameData, 0, byteCount);
            }

            public void Dispose()
            {
                try
                {
                    stdin?.Flush();
                    stdin?.Close();
                }
                catch
                {
                }

                if (process != null)
                {
                    string stderr = process.StandardError.ReadToEnd();
                    if (!process.WaitForExit(5000))
                    {
                        process.Kill();
                    }

                    if (!string.IsNullOrEmpty(stderr))
                    {
                        Plugin.logger.LogInfo($"[PlaybackCameraRecorder][ffmpeg] {stderr}");
                    }

                    process.Dispose();
                }

                stdin = null;
                process = null;
            }
        }
    }
}