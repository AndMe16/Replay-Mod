using Imui.Controls;
using Imui.Core;
using Imui.Rendering;
using Replay_Mod;
using ReplayMod.PlaybackManager;
using System;
using UnityEngine;
using ZeepSDK.UI;

namespace ReplayMod.GUIDrawer
{
    public class MyGUIDrawer : IZeepGUIDrawer
    {
        public bool _SavesWindowOpen = false;

        public bool _PlaybackWindowOpen = false;

        private string[] values = [];

        private string selectedRecording = null;

        private int _selectedIndex = -1;

        public void OnZeepGUI(ImGui gui)
        {
            SavesWindow(gui);

            PlaybackWindowOpen(gui);
        }

        private void SavesWindow(ImGui gui)
        {
            if (_SavesWindowOpen && gui.BeginWindow("Editor Recordings", ref _SavesWindowOpen, (500, 500)))
            {
                ListOfRecordings(gui);

                RecordingInfo(gui);
                gui.EndWindow();
            }
        }

        private void PlaybackWindowOpen(ImGui gui)
        {
            ImRect rect = new ImRect(0,0, Screen.width*0.3f, Screen.height * 0.2f);

            if (_PlaybackWindowOpen && gui.BeginWindow("Playback Controls", ref _PlaybackWindowOpen, rect, ImWindowFlag.NoCloseButton))
            {
                var manager = PlaybackManager.PlaybackManager.Instance;

                gui.Separator("Playback");

                CustomSliderHeader(gui, "Time", manager._currentSessionTime);
                if(gui.Slider(ref manager._currentSessionTime, 0, ((float)manager.Session.duration.TotalSeconds)))
                {
                    manager.ScrubToTime(manager._currentSessionTime);
                }

                gui.BeginHorizontal();

                var playPauseIcon = manager.IsFollowingTimeline ? "\u23F8" : "\u25B6";

                if (gui.Button(playPauseIcon, size: new ImSize(gui.GetLayoutWidth() * 0.1f, gui.GetRowHeight())))
                {
                    if (manager.IsFollowingTimeline)
                    {
                        manager.StopFollowingTimeline();
                    }
                    else
                    {
                        manager.StartFollowingTimeline();
                    }
                }

                gui.AddSpacing();

                float speed = manager.SpeedMultiplier;
                gui.NumericEdit(ref speed, step: 0.25f, size: new ImSize(gui.GetLayoutWidth()*0.3f, gui.GetRowHeight()), flags: ImNumericEditFlag.PlusMinus, format: "F2" , min:0.25f, max: 10);
                manager.SpeedMultiplier = speed;

                gui.AddSpacing();

                if (!manager.IsFollowingTimeline)
                {
                    if (gui.Button("<", ImSizeMode.Auto))
                    {
                        manager.StepBackward();
                        manager.UpdateGhostFromTimeline(manager._currentSessionTime);
                    }
                    if (gui.Button(">", ImSizeMode.Auto))
                    {
                        manager.StepForward();
                        manager.UpdateGhostFromTimeline(manager._currentSessionTime);
                    }
                }

                gui.AddSpacing();

                if (gui.Checkbox(ref manager.followCamera, "Follow Camera", ImSizeMode.Auto))
                {
                    manager.ToggledFollowCamera();
                }

                gui.EndHorizontal();

                gui.Separator("Recording");

                gui.BeginHorizontal();

                var playbackRecorder = RecorderLifecycleBridge.RecorderLifecycleBridge.playbackCameraRecorder;

                if (playbackRecorder != null)
                {

                    if (playbackRecorder?.recording == true)
                    {
                        if (gui.Button("\u23F9", size: new ImSize(gui.GetLayoutWidth() * 0.1f, gui.GetRowHeight())))
                        {
                            playbackRecorder.StopRecording();
                        }

                    }
                    else
                    {
                        if (gui.Button("\u23FA", new ImSize(gui.GetLayoutWidth() * 0.1f, gui.GetRowHeight())))
                        {
                            playbackRecorder.StartRecording();
                        }
                    }
                }

                string timeSinceStartRecordingString;

                if (playbackRecorder.recordingTime != TimeSpan.Zero)
                {
                    timeSinceStartRecordingString = playbackRecorder.recordingTime.ToString(@"hh\:mm\:ss");
                }
                else
                {
                    timeSinceStartRecordingString = "--:--:--";
                }

                gui.TextEditNonEditable(timeSinceStartRecordingString, size: new ImSize(gui.GetLayoutWidth() * 0.2f, gui.GetRowHeight()));

                gui.EndHorizontal();

                gui.EndWindow();
            }
        }

        private void ListOfRecordings(ImGui gui)
        {
            gui.Separator("List of recordings");

            gui.BeginList((gui.GetLayoutWidth(), ImList.GetEnclosingHeight(gui, gui.GetRowsHeightWithSpacing(5))));

            for (int i = 0; i < values.Length; ++i)
            {

                if (gui.ListItem(ref _selectedIndex, i, values[i]))
                {
                    Plugin.logger.LogInfo($"[GUIDrawer] Clicked on {values[i]}");
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


                    gui.TextEditNonEditable(info, (gui.GetLayoutWidth(), gui.GetTextLineHeight() * 4.5f), true);
                }
                else
                {
                    gui.TextEditNonEditable("Failed to load recording session.", (gui.GetLayoutWidth(), gui.GetTextLineHeight() * 1.5f), true);
                }

                gui.AddSpacing();

                gui.BeginHorizontal();
                if (gui.Button("Open", ImSizeMode.Auto))
                {
                    Plugin.logger.LogInfo($"[GUIDrawer] Opening recording {selectedRecording}");
                    RecorderLifecycleBridge.RecorderLifecycleBridge.OpenPlaybackScene(selectedRecording);
                    _SavesWindowOpen = false;
                }

                gui.AddSpacing();

                if (gui.Button("Delete", ImSizeMode.Auto))
                {
                    Plugin.logger.LogInfo($"[GUIDrawer] Deleting recording {selectedRecording}");
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

        public void OpenPlaybackWindow()
        {
            _PlaybackWindowOpen = true;
        }

        public void ClosePlaybackWindow()
        {
            _PlaybackWindowOpen = false;
        }

        public void RefreshUI()
        {
            RefreshFiles();
            selectedRecording = null;
            _selectedIndex = -1;
        }

        public  void CustomSliderHeader(ImGui gui,
                                        ReadOnlySpan<char> label,
                                        float value)
        {
            gui.AddSpacingIfLayoutFrameNotEmpty();
            gui.BeginHorizontal();

            var rowHeight = gui.GetRowHeight();
            var height = rowHeight * gui.Style.Slider.HeaderScale;
            var rect = gui.AddLayoutRect(gui.GetLayoutWidth(), height);
            var barHeight = gui.Style.Slider.BarThickness * rowHeight;
            var padding = (rowHeight - barHeight) * 0.5f;
            var fontSize = gui.TextDrawer.GetFontSizeFromLineHeight(height);

            // (artem-s): align with slider's bar
            rect.X += padding;
            rect.W -= padding * 2;

            // (artem-s): shift rect down by spacing value so there is no gap between header and slider itself
            rect.Y -= gui.Style.Layout.Spacing;

            var textSettings = new ImTextSettings(fontSize, 0.0f, 1.0f, overflow: ImTextOverflow.Ellipsis);
            gui.Text(label, textSettings, rect);

            var time = TimeSpan.FromSeconds(value);

            string valueFormatted = value % 1 == 0
                ? time.ToString(@"hh\:mm\:ss")
                : time.ToString(@"hh\:mm\:ss\.f");
            textSettings.Align.X = 1.0f;
            gui.Text(valueFormatted, textSettings, rect);

            gui.EndHorizontal();
        }

    }
}