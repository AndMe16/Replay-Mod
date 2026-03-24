using Replay_Mod;
using ZeepSDK.Storage;

namespace ReplayMod.FilesManager
{
    internal static class FilesManager
    {
        public static void SaveRecordingSession(IModStorage editorRecorderStorage, RecordManager.RecordingSession session, string name)
        {
            editorRecorderStorage.SaveToJson(name, session);
        }

        public static RecordManager.RecordingSession LoadRecordingSession(IModStorage editorRecorderStorage, string name)
        {
            RecordManager.RecordingSession session = null;
            if (Plugin.Storage.JsonFileExists(name))
            {
                session = Plugin.Storage.LoadFromJson<RecordManager.RecordingSession>(name);
            }
            else
            {
                Plugin.logger.LogError($"Recording file {name} does not exist.");
            }

            return session;
        }

        public static void DeleteRecordingSession(IModStorage editorRecorderStorage, string name)
        {
            if (Plugin.Storage.JsonFileExists(name))
            {
                Plugin.Storage.DeleteJsonFile(name);
            }
            else
            {
                Plugin.logger.LogError($"Recording file {name} does not exist.");
            }
        }
    }
}
