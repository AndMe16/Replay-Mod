using Imui.Controls;
using Imui.Core;
using ZeepSDK.UI;
using ZeepSDK.LevelEditor;

namespace ReplayMod.ToolbarDrawer
{
    public class MyToolbarDrawer : IZeepToolbarDrawer
    {
        public string MenuTitle => "EditorRecorder";

        public void DrawMenuItems(ImGui gui)
        {
            if (LevelEditorApi.IsInLevelEditor)
            {
                if (gui.Menu("Start/Stop Recording"))
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
        }
    }
}