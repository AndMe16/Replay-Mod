using BepInEx.Configuration;
using Replay_Mod;
using ReplayMod.PlaybackManager;
using ReplayMod.RecorderLifecycleBridge;
using System;
using System.IO;
using UnityEngine;
using ZeepSDK.LevelEditor;
using ZeepSDK.Messaging;

public class ModConfig : MonoBehaviour
{
    public static ConfigEntry<KeyCode> RecordBuilding;

    public static ConfigEntry<KeyCode> TogglePlayback;
    public static ConfigEntry<KeyCode> StepBack;
    public static ConfigEntry<KeyCode> StepForward;
    public static ConfigEntry<KeyCode> FollowCamera;
    public static ConfigEntry<KeyCode> HideGUI;

    public static ConfigEntry<KeyCode> ToggleRecording;
    public static ConfigEntry<string> RecordingsSavePath;

    public static void Initialize(ConfigFile config)
    {
        RecordBuilding = config.Bind("1. Building", "Start/Stop Building Recording", KeyCode.None,
            "Key to Start/Stop recording during building");

        TogglePlayback = config.Bind("2. Playback", "Play/Pause Playback", KeyCode.P,
            "Key to Play/Pause the playback of the recorded building process");

        StepBack = config.Bind("2. Playback", "Step Back in Playback", KeyCode.LeftArrow,
            "Key to step back in the playback of the recorded building process");

        StepForward = config.Bind("2. Playback", "Step Forward in Playback", KeyCode.RightArrow,
            "Key to step forward in the playback of the recorded building process");

        FollowCamera = config.Bind("2. Playback", "Follow Camera Toggle", KeyCode.F,
            "Key to toggle the camera follow mode during playback");

        HideGUI = config.Bind("2. Playback", "Hide GUI Toggle", KeyCode.H,
            "Key to toggle the visibility of the mod's GUI during playback");

        ToggleRecording = config.Bind("3. Recording", "Start/Stop camera recording", KeyCode.R,
            "Key to Start/Stop camera recording during playback");

        RecordingsSavePath = config.Bind("3. Recording", "Recordings Save Folder", Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Zeepkist",
            "Mods",
            "Replay Mod",
            "Recordings"
            ),
        "Path of the folder where the recording sessions will be saved ");

        RecordingsSavePath.SettingChanged += OnSettingsChanged;

    }

    private static void OnSettingsChanged(object sender, System.EventArgs e)
    {
        var configEntry = sender as ConfigEntryBase;

        Plugin.logger.LogInfo($"Setting changed: {configEntry.Definition.Key}");

        if (configEntry == RecordingsSavePath)
        {
            if (LevelEditorApi.IsInLevelEditor && PlaybackManager.Instance.IsPlaying)
            {
                RecorderLifecycleBridge.playbackCameraRecorder.outputFolder = RecordingsSavePath.Value;
            }

        }
    }
}