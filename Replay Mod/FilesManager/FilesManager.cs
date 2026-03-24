using ZeepSDK.Storage;

namespace ReplayMod.FilesManager
{
    internal static class FilesManager
    {
        public static void SaveRecordingSession(IModStorage editorRecorderStorage, RecordManager.RecordingSession session, string name)
        {
            editorRecorderStorage.SaveToJson(name, session);
        }

    }
}
