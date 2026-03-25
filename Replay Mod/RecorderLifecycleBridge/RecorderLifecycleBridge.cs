using Replay_Mod;
using ReplayMod.GUIDrawer;
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

        public static void Initialize()
        {
            if (_initialized)
                return;

            _initialized = true;

            SceneManager.sceneLoaded += OnEnteredLevelEditor;
            SceneManager.sceneUnloaded += OnExitedLevelEditor;

            Plugin.logger.LogInfo("[EditorRecorder] Subscribed to level editor lifecycle events.");
        }

        private static void OnEnteredLevelEditor(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == "LevelEditor2")
            {
                if (isPlayback)
                {
                    isPlayback = false;
                    DisableOriginalUI();
                    isInPlaybackScene = true;
                    new GameObject("PauseMenuHandler").AddComponent<PauseMenuHandler.PauseMenuHandler>();
                    LoadAndBeginPlayback();
                }
            }            
        }

        private static void OnExitedLevelEditor(Scene scene)
        {
            if (scene.name == "LevelEditor2")
            {
                isInPlaybackScene = false;
                if (PlaybackManager.PlaybackManager.Instance.IsPlaying)
                {
                    PlaybackManager.PlaybackManager.Instance.StopPlayback();
                }
                GameObject.Destroy(GameObject.Find("PauseMenuHandler"));
            }
        }

        public static void Shutdown()
        {
            if (!_initialized)
                return;

            _initialized = false;

            SceneManager.sceneLoaded -= OnEnteredLevelEditor;
            SceneManager.sceneUnloaded -= OnExitedLevelEditor;

            Plugin.logger.LogInfo("[EditorRecorder] Unsubscribed from level editor lifecycle events.");
        }

        public static void OpenPlaybackScene(string recordingName)
        {
            isPlayback = true;
            SceneManager.LoadScene("LevelEditor2");
            currentRecordingName = recordingName;
        }

        private static void LoadAndBeginPlayback()
        {
            var session = FilesManager.FilesManager.LoadRecordingSession(Plugin.Storage, currentRecordingName);

            var central = GameObject.FindObjectOfType<LEV_LevelEditorCentral>();
            if (central != null)
            {
                PlaybackManager.PlaybackManager.Instance.BeginPlayback(session, central.undoRedo);
            }
        }

        private static void  DisableOriginalUI()
        {

            LEV_LevelEditorCentral levelEditorCentral = GameObject.FindObjectOfType<LEV_LevelEditorCentral>();

            if (levelEditorCentral != null)
            {
                levelEditorCentral.cam.cameraCamera.cullingMask = levelEditorCentral.saveload.cameraTakeScreenshot;
                levelEditorCentral.cam.cameraCamera.rect = new Rect(0f, 0f, 1f, 1f);
                levelEditorCentral.cam.skyCamera.rect = new Rect(0f, 0f, 1f, 1f);
                levelEditorCentral.cam.gizmoCamera.rect = new Rect(0f, 0f, 1f, 1f);

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
