using Imui.Controls;
using Imui.Core;
using Replay_Mod;
using System;
using System.IO;
using System.Linq;
using ZeepSDK.UI;

namespace ReplayMod.GUIDrawer
{
    public class MyGUIDrawer : IZeepGUIDrawer
    {
        public bool _SavesWindowOpen = false;

        private string[] values = [];

        public void OnZeepGUI(ImGui gui)
        {
            if (_SavesWindowOpen && gui.BeginWindow("Editor Recordings", ref _SavesWindowOpen, (400, 300)))
            {
                Plugin.logger.LogInfo($"Found {values.Length} files in the folder.");
                gui.BeginList((gui.GetLayoutWidth(), gui.GetLayoutHeight()));

                for (int i = 0; i < values.Length; ++i)
                {
                    var wasSelected = false;

                    if (gui.ListItem(wasSelected, values[i]))
                    {
                        Plugin.logger.LogInfo($"Clicked on {values[i]}");
                    }
                }

                gui.EndList();
                gui.EndWindow();
            }
        }

        private void RefreshFiles()
        {
            string folderPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Zeepkist",
            "Mods",
            Plugin.Instance.Info.Metadata.GUID);

            if (!Directory.Exists(folderPath))
            {
                values = System.Array.Empty<string>();
                return;
            }

            values = Directory.GetFiles(folderPath)
                              .Select(Path.GetFileName)
                              .ToArray();
        }

        public void OpenSavesWindow()
        {
            RefreshFiles();
            _SavesWindowOpen = true;
        }
    }
}