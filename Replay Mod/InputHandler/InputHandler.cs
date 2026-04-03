using Replay_Mod;
using ReplayMod.PlaybackManager;
using UnityEngine;
using ZeepSDK.LevelEditor;

namespace ReplayMod.InputHandler
{
    public class InputHandler : MonoBehaviour
    {
        RecordManager.RecordManager recordManager;
        PlaybackManager.PlaybackManager playbackManager;


        void Start()
        {
            recordManager = RecordManager.RecordManager.Instance;
            playbackManager = PlaybackManager.PlaybackManager.Instance;
        }

        void Update()
        {
            if (Input.GetKeyDown(ModConfig.RecordBuilding.Value))
            {
                if (LevelEditorApi.IsInLevelEditor && !playbackManager.IsPlaying)
                {
                    if (recordManager.IsRecording)
                    {
                        recordManager.StopRecording();
                    }
                    else
                    {
                        recordManager.StartRecording();
                    }
                }
            }
            if (Input.GetKeyDown(ModConfig.TogglePlayback.Value))
            {
                if (LevelEditorApi.IsInLevelEditor && playbackManager.IsPlaying)
                {
                    if (playbackManager.IsFollowingTimeline)
                    {
                        playbackManager.StopFollowingTimeline();
                    }
                    else
                    {
                        playbackManager.StartFollowingTimeline();
                    }
                }
            }
            if (Input.GetKeyDown(ModConfig.StepBack.Value))
            {
                if (LevelEditorApi.IsInLevelEditor && playbackManager.IsPlaying && !playbackManager.IsFollowingTimeline)
                {
                    playbackManager.StepBackward();
                    playbackManager.UpdateGhostFromTimeline(playbackManager._currentSessionTime);
                }
            }
            if (Input.GetKeyDown(ModConfig.StepForward.Value))
            {
                if (LevelEditorApi.IsInLevelEditor && playbackManager.IsPlaying && !playbackManager.IsFollowingTimeline)
                {
                    playbackManager.StepForward();
                    playbackManager.UpdateGhostFromTimeline(playbackManager._currentSessionTime);
                }
            }
            if (Input.GetKeyDown(ModConfig.FollowCamera.Value))
            {
                if (LevelEditorApi.IsInLevelEditor && playbackManager.IsPlaying)
                {
                    playbackManager.followCamera = !playbackManager.followCamera;
                    playbackManager.ToggledFollowCamera();
                }
            }
            if (Input.GetKeyDown(ModConfig.HideGUI.Value))
            {
                if (LevelEditorApi.IsInLevelEditor && playbackManager.IsPlaying)
                {
                    Plugin._guiDrawer._PlaybackWindowOpen = !Plugin._guiDrawer._PlaybackWindowOpen;
                }
            }
            if (Input.GetKeyDown(ModConfig.ToggleRecording.Value))
            {
                if (LevelEditorApi.IsInLevelEditor && playbackManager.IsPlaying)
                {
                    var playbackRecorder = RecorderLifecycleBridge.RecorderLifecycleBridge.playbackCameraRecorder;
                    if (playbackRecorder != null)
                    {
                        if (playbackRecorder?.recording == true)
                        {
                            playbackRecorder.StopRecording();

                        }
                        else
                        {
                            playbackRecorder.StartRecording();
                        }
                    }
                }
            }
        }
    }
}