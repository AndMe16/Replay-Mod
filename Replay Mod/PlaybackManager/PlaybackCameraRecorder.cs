using Replay_Mod;
using System.Collections;
using System.IO;
using UnityEngine;


namespace ReplayMod.PlaybackManager
{
    public class PlaybackCameraRecorder : MonoBehaviour
    {
        [Header("References")]
        public LEV_MoveCamera cam;
        public Camera mainCamera;
        public Camera skyCamera;

        [Header("Recording")]
        public int width = 1920;
        public int height = 1080;
        public int targetFPS = 60;
        public string outputFolder = "Recordings";

        private bool recording;
        private int frame;
        private RenderTexture renderTexture;

        //void Start()
        //{
        //    Directory.CreateDirectory(Path.Combine(Application.dataPath, outputFolder));
        //}

        void Update()
        {
            // Toggle recording (example key)
            if (Input.GetKeyDown(KeyCode.F9))
            {
                if (!recording)
                    StartRecording();
                else
                    StopRecording();
            }
        }

        public void StartRecording()
        {
            recording = true;
            frame = 0;

            Plugin.logger.LogInfo("[PlaybackCameraRecorder] Recording started");

            Time.captureFramerate = targetFPS;

            renderTexture = new RenderTexture(width, height, 32);

            StartCoroutine(CaptureLoop());
        }

        public void StopRecording()
        {
            recording = false;

            Plugin.logger.LogInfo("[PlaybackCameraRecorder] Recording stopped");

            Time.captureFramerate = 0;

            if (renderTexture != null)
            {
                renderTexture.Release();
            }
        }

        private IEnumerator CaptureLoop()
        {
            string fullPath = outputFolder;

            while (recording)
            {
                yield return new WaitForEndOfFrame();

                if (skyCamera != null)
                {
                    skyCamera.targetTexture = renderTexture;
                    skyCamera.Render();
                    skyCamera.targetTexture = null;
                }

                mainCamera.targetTexture = renderTexture;
                mainCamera.Render();
                mainCamera.targetTexture = null;

                Texture2D tex = new Texture2D(width, height, TextureFormat.RGB24, false);
                RenderTexture.active = renderTexture;
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply();

                byte[] bytes = tex.EncodeToJPG(95);
                string file = Path.Combine(fullPath, $"frame_{frame:D05}.jpg");

                Plugin.Storage.SaveToFile(file, bytes);

                Destroy(tex);

                frame++;
            }
        }
    }
}