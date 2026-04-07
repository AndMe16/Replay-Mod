using Replay_Mod;
using ReplayMod.PlaybackManager;
using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using ZeepSDK.LevelEditor;

namespace ReplayMod.RecorderLifecycleBridge
{
    public static class RecorderLifecycleBridge
    {
        private static bool _initialized;

        private static bool isPlayback = false;
        private static bool isInPlaybackScene = false;

        private static string currentRecordingName = null;

        private static LEV_LevelEditorCentral central;

        public static PlaybackCameraRecorder playbackCameraRecorder;

        public static void Initialize()
        {
            if (_initialized)
                return;

            _initialized = true;

            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;

            Plugin.logger.LogInfo("[RecorderLifecycleBridge] Subscribed to level editor lifecycle events.");
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == "LevelEditor2")
            {
                central = GameObject.FindObjectOfType<LEV_LevelEditorCentral>();

                if (central == null)
                    return;

                if (RecordManager.RecordManager.Instance.IsRecording && RecordManager.RecordManager.Instance.IsPaused)
                {
                    Plugin.logger.LogInfo("[RecorderLifecycleBridge] Entered level editor while paused, resuming recording");
                    RecordManager.RecordManager.Instance.ResumeRecording();
                }

                if (isPlayback)
                {
                    isPlayback = false;

                    DisableSelection();
                    DisableOriginalUI();
                    DisableTools();
                    isInPlaybackScene = true;
                    new GameObject("PauseMenuHandler").AddComponent<PauseMenuHandler.PauseMenuHandler>();
                    new GameObject("PlaybackController").AddComponent<PlaybackController>();
                    playbackCameraRecorder = new GameObject("PlaybackCameraRecorder").AddComponent<PlaybackCameraRecorder>();

                    playbackCameraRecorder.cam = central.cam;
                    playbackCameraRecorder.mainCamera = central.cam.cameraCamera;
                    playbackCameraRecorder.skyCamera = central.cam.skyCamera;

                    LoadAndBeginPlayback();
                }
                new GameObject("ReplayModInputHandler").AddComponent<InputHandler.InputHandler>();
            }

            else if (scene.name == "GameScene" && LevelEditorApi.IsTestingLevel && RecordManager.RecordManager.Instance.IsRecording)
            {
                Plugin.logger.LogInfo("[RecorderLifecycleBridge] Entered play mode while recording, pausing recording");
                RecordManager.RecordManager.Instance.PauseRecording();
            }

            else
            {
                if (RecordManager.RecordManager.Instance.IsRecording)
                {
                    Plugin.logger.LogInfo("[RecorderLifecycleBridge] Loaded another scene while recording editor");
                    RecordManager.RecordManager.Instance.StopRecording();
                }
            }
        }

        private static void DisableSelection()
        {
            central.click.onClickBuilding.RemoveAllListeners();
            central.click.onClickNothing.RemoveAllListeners();
        }

        private static void DisableTools()
        {
            central.tool.DisableAllTools();
        }

        private static void OnSceneUnloaded(Scene scene)
        {
            if (scene.name == "LevelEditor2")
            {
                if (isInPlaybackScene)
                {
                    isInPlaybackScene = false;
                    central = null;

                    if (PlaybackManager.PlaybackManager.Instance.IsPlaying)
                    {
                        PlaybackManager.PlaybackManager.Instance.StopPlayback();
                    }
                    GameObject.Destroy(GameObject.Find("PauseMenuHandler"));
                }
            }
        }

        public static void Shutdown()
        {
            if (!_initialized)
                return;

            _initialized = false;

            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;

            Plugin.logger.LogInfo("[RecorderLifecycleBridge] Unsubscribed from level editor lifecycle events.");
        }

        public static void OpenPlaybackScene(string recordingName)
        {
            if (LevelEditorApi.IsInLevelEditor)
            {
                if (central == null)
                {
                    Plugin.logger.LogWarning("[RecorderLifecycleBridge] Central manager is null when trying to open playback scene. This should not happen.");
                    return;
                }

                central.saveload.SaveBackup(true);

                if (central.manager.unsavedContent)
                {
                    central.unsavedContentPopup.ShouldQuit.RemoveAllListeners();
                    central.unsavedContentPopup.ShouldQuit.AddListener(delegate (bool quit)
                    {
                        if (quit)
                        {
                            central.saveload.SaveBackup(true);
                            central.manager.validated = false;
                            central.manager.unsavedContent = false;
                            central.manager.validationTime = 0f;
                            central.manager.testLevelName = "";
                            central.manager.ResetAll();
                            central.manager.exitFromLevelEditor = true;
                            isPlayback = true;
                            SceneManager.LoadScene("LevelEditor2");
                            currentRecordingName = recordingName;
                        }
                    });
                    central.unsavedContentPopup.Open(true);
                    Plugin._guiDrawer._SavesWindowOpen = false;
                    return;
                }
                central.saveload.SaveBackup(true);
                central.manager.validated = false;
                central.manager.unsavedContent = false;
                central.manager.validationTime = 0f;
                central.manager.testLevelName = "";
                central.manager.ResetAll();
                central.manager.exitFromLevelEditor = true;
                isPlayback = true;
                SceneManager.LoadScene("LevelEditor2");
                currentRecordingName = recordingName;
                Plugin._guiDrawer._SavesWindowOpen = false;
            }

            else
            {
                isPlayback = true;
                SceneManager.LoadScene("LevelEditor2");
                currentRecordingName = recordingName;
                Plugin._guiDrawer._SavesWindowOpen = false;
            }

        }

        private static void LoadAndBeginPlayback()
        {
            var session = FilesManager.FilesManager.LoadRecordingSession(Plugin.Storage, currentRecordingName);
            PlaybackManager.PlaybackManager.Instance.BeginPlayback(session);
        }

        private static void DisableOriginalUI()
        {

            central = GameObject.FindObjectOfType<LEV_LevelEditorCentral>();

            if (central != null)
            {
                central.cam.cameraCamera.cullingMask = central.saveload.cameraTakeScreenshot;
                central.cam.cameraCamera.rect = new Rect(0f, 0f, 1f, 1f);
                central.cam.skyCamera.rect = new Rect(0f, 0f, 1f, 1f);
                central.cam.gizmoCamera.rect = new Rect(0f, 0f, 1f, 1f);

                string[] paths =
                [
                    "Level Editor Central/Canvas/Inspector",
                    "Level Editor Central/Canvas/Toolbar",
                    "Level Editor Central/Canvas/GameView"
                ];

                foreach (var path in paths)
                {
                    var obj = GameObject.Find(path);
                    obj?.SetActive(false);
                }
            }
        }
    }
}
