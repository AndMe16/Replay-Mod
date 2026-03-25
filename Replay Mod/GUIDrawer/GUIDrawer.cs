using BugsnagUnity.Payload;
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

        private string selectedRecording = null;

        private int _selectedIndex = -1;

        public void OnZeepGUI(ImGui gui)
        {
            
            if (_SavesWindowOpen && gui.BeginWindow("Editor Recordings", ref _SavesWindowOpen, (500, 500)))
            {
                ListOfRecordings(gui);

                RecordingInfo(gui);
                gui.EndWindow();
            }
        }

        private void ListOfRecordings(ImGui gui)
        {
            gui.Separator("List of recordings");

            gui.BeginList((gui.GetLayoutWidth(), ImList.GetEnclosingHeight(gui, gui.GetRowsHeightWithSpacing(5))));

            for (int i = 0; i < values.Length; ++i)
            {

                if (gui.ListItem(ref _selectedIndex,i, values[i]))
                {
                    Plugin.logger.LogInfo($"Clicked on {values[i]}");
                    selectedRecording = values[i];
                }
            }

            gui.EndList();
            
        }

        private void RecordingInfo(ImGui gui)
        {
            gui.Separator("Recording info");

            if (selectedRecording != null)
            {
                var session = FilesManager.FilesManager.LoadRecordingSession(Plugin.Storage, selectedRecording);

                if (session != null)
                {
                    string info = $"Name: {selectedRecording}\n" +
                              $"Date: {session.savingTime:G}\n" +
                              $"Duration: {session.duration:hh':'mm':'ss}\n" +
                              $"Actions recorded: {session.eventCount}";


                    gui.TextEditNonEditable(info, (gui.GetLayoutWidth(),gui.GetTextLineHeight()*4.5f), true);
                }
                else
                {
                    gui.TextEditNonEditable("Failed to load recording session.", (gui.GetLayoutWidth(),gui.GetTextLineHeight()*1.5f), true);
                }

                gui.AddSpacing();

                gui.BeginHorizontal();
                if (gui.Button("Open", ImSizeMode.Auto))
                {
                    Plugin.logger.LogInfo($"Opening recording {selectedRecording}");
                    RecorderLifecycleBridge.RecorderLifecycleBridge.OpenPlaybackScene(selectedRecording);
                    _SavesWindowOpen = false;
                }

                gui.AddSpacing();

                if (gui.Button("Delete", ImSizeMode.Auto))
                {
                    Plugin.logger.LogInfo($"Deleting recording {selectedRecording}");
                    FilesManager.FilesManager.DeleteRecordingSession(Plugin.Storage, selectedRecording);
                    RefreshUI();
                }

                gui.EndHorizontal();
            }
        }

        private void RefreshFiles()
        {
            values = FilesManager.FilesManager.GetAllRecordingSessions(Plugin.Storage);
        }

        public void OpenSavesWindow()
        {
            RefreshUI();
            _SavesWindowOpen = true;
        }

        public void RefreshUI()
        {
            RefreshFiles();
            selectedRecording = null;
            _selectedIndex = -1;
        }
    }
}