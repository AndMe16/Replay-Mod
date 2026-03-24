using Replay_Mod;
using ZeepSDK.LevelEditor;

namespace ReplayMod.RecorderLifecycleBridge
{
    public static class RecorderLifecycleBridge
    {
        private static bool _initialized;

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
            RecordManager.RecordManager.Instance.StartRecording();
        }

        private static void OnExitedLevelEditor()
        {
            Plugin.logger.LogInfo("[EditorRecorder] Exited level editor.");
            RecordManager.RecordManager.Instance.StopRecording();
        }
    }
}
