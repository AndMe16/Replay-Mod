using Replay_Mod;
using ReplayMod.PlaybackManager;
using UnityEngine;
using UnityEngine.SceneManagement;

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

            SceneManager.sceneLoaded += OnEnteredLevelEditor;
            SceneManager.sceneUnloaded += OnExitedLevelEditor;

            Plugin.logger.LogInfo("[RecorderLifecycleBridge] Subscribed to level editor lifecycle events.");
        }

        private static void OnEnteredLevelEditor(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == "LevelEditor2")
            {
                if (isPlayback)
                {
                    isPlayback = false;

                    central = GameObject.FindObjectOfType<LEV_LevelEditorCentral>();

                    if (central == null)
                        return;
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

        private static void OnExitedLevelEditor(Scene scene)
        {
            if (scene.name == "LevelEditor2")
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

        public static void Shutdown()
        {
            if (!_initialized)
                return;

            _initialized = false;

            SceneManager.sceneLoaded -= OnEnteredLevelEditor;
            SceneManager.sceneUnloaded -= OnExitedLevelEditor;

            Plugin.logger.LogInfo("[RecorderLifecycleBridge] Unsubscribed from level editor lifecycle events.");
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
            PlaybackManager.PlaybackManager.Instance.BeginPlayback(session);
        }

        private static void DisableOriginalUI()
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
