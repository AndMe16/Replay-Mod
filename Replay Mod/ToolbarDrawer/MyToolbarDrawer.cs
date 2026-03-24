using BugsnagUnity.Payload;
using Imui.Controls;
using Imui.Core;
using Replay_Mod;
using ReplayMod.RecordManager;
using UnityEngine;
using ZeepSDK.LevelEditor;
using ZeepSDK.UI;

namespace ReplayMod.ToolbarDrawer
{
    public class MyToolbarDrawer : IZeepToolbarDrawer
    {
        public string MenuTitle => "EditorRecorder";

        public void DrawMenuItems(ImGui gui)
        {
            if (LevelEditorApi.IsInLevelEditor)
            {
                if (gui.Menu("Start/Stop Recording", RecordManager.RecordManager.Instance.IsRecording))
                {
                    if (RecordManager.RecordManager.Instance.IsRecording)
                    {
                        RecordManager.RecordManager.Instance.StopRecording();
                    }
                    else
                    {
                        RecordManager.RecordManager.Instance.StartRecording();
                    }
                }

                gui.Separator();

                if (gui.Menu("Start/Stop Playback", PlaybackManager.PlaybackManager.Instance.IsPlaying))
                {

                    if (PlaybackManager.PlaybackManager.Instance.IsPlaying)
                    {
                        PlaybackManager.PlaybackManager.Instance.StopPlayback();
                    }
                    else
                    {
                        var central = GameObject.FindObjectOfType<LEV_LevelEditorCentral>();
                        if (central != null)
                        {
                            PlaybackManager.PlaybackManager.Instance.BeginPlayback(RecordManager.RecordManager.Instance.CurrentSession, central.undoRedo);
                        }
                    }
                }

                if (PlaybackManager.PlaybackManager.Instance.IsPlaying)
                {
                    if (gui.Menu("Step Forward"))
                    {
                        PlaybackManager.PlaybackManager.Instance.StepForward();
                    }

                    if (gui.Menu("Reset Playback"))
                    {
                        PlaybackManager.PlaybackManager.Instance.ResetToCleanEditor();
                    }
                }
            }

            gui.Separator();

            if (gui.Menu("Recordings", Plugin._guiDrawer._SavesWindowOpen))
            {
                Plugin._guiDrawer.OpenSavesWindow();
            }
        }
    }
}