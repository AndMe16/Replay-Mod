using Replay_Mod;
using ReplayMod.GUIDrawer;
using UnityEngine;
using UnityEngine.SceneManagement;
using ZeepSDK.LevelEditor;

namespace ReplayMod.RecorderLifecycleBridge
{
    public static class RecorderLifecycleBridge
    {
        private static bool _initialized;

        private static bool isPlayback = false;

        public static void Initialize()
        {
            if (_initialized)
                return;

            _initialized = true;

            LevelEditorApi.EnteredLevelEditor += OnEnteredLevelEditor;
            LevelEditorApi.ExitedLevelEditor += OnExitedLevelEditor;

            Plugin.logger.LogInfo("[EditorRecorder] Subscribed to level editor lifecycle events.");
        }

        public static void Shutdown()
        {
            if (!_initialized)
                return;

            _initialized = false;

            LevelEditorApi.EnteredLevelEditor -= OnEnteredLevelEditor;
            LevelEditorApi.ExitedLevelEditor -= OnExitedLevelEditor;

            Plugin.logger.LogInfo("[EditorRecorder] Unsubscribed from level editor lifecycle events.");
        }

        private static void OnEnteredLevelEditor()
        {
            Plugin.logger.LogInfo("[EditorRecorder] Entered level editor.");

            if (isPlayback)
            {
                Plugin.logger.LogInfo("[EditorRecorder] Starting playback.");
                DisableOriginalUI();
                isPlayback = false;
            }

            // RecordManager.RecordManager.Instance.StartRecording();
        }

        private static void OnExitedLevelEditor()
        {
            Plugin.logger.LogInfo("[EditorRecorder] Exited level editor.");
            // RecordManager.RecordManager.Instance.StopRecording();
        }

        public static void OpenPlaybackScene(string recordingName)
        {
            isPlayback = true;
            SceneManager.LoadScene("LevelEditor2");
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
