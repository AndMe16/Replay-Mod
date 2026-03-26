using Replay_Mod;
using System;
using System.IO;
using System.Linq;
using ZeepSDK.Storage;

namespace ReplayMod.FilesManager
{
    internal static class FilesManager
    {
        static readonly string folderPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Zeepkist",
            "Mods",
            Plugin.Instance.Info.Metadata.GUID);

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

        public static string[] GetAllRecordingSessions(IModStorage editorRecorderStorage)
        {

            if (!Directory.Exists(folderPath))
            {
                return [];
            }

            return [.. Directory.GetFiles(folderPath).Select(Path.GetFileNameWithoutExtension)];
        }
    }
}
