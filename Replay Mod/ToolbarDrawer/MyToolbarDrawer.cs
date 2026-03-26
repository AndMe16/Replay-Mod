using Imui.Controls;
using Imui.Core;
using Replay_Mod;
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
                if (!PlaybackManager.PlaybackManager.Instance.IsPlaying)
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
                }
                else
                {
                    if (gui.Menu("Step Forward"))
                    {
                        PlaybackManager.PlaybackManager.Instance.StepForward();
                    }

                    if (gui.Menu("Follow Timeline", PlaybackManager.PlaybackManager.Instance.IsFollowingTimeline))
                    {
                        if (PlaybackManager.PlaybackManager.Instance.IsFollowingTimeline)
                        {
                            PlaybackManager.PlaybackManager.Instance.StopFollowingTimeline();
                        }
                        else
                        {
                            PlaybackManager.PlaybackManager.Instance.StartFollowingTimeline();
                        }
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
