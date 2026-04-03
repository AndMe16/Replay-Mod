using Replay_Mod;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ReplayMod.RecordManager
{
    [Serializable]
    public class RecordedSingleChange
    {
        public string uid;

        // Any complains with Yannic and this weird undo redo system :)
        public string beforeJson;
        public string afterJson;

        public int intBefore;
        public int intAfter;

        public string customSkyboxBefore;
        public string customSkyboxAfter;

        public int int2Before;
        public int int2After;

        public bool boolBefore;
        public bool boolAfter;

        public bool changeBool1;

        public bool wasAdded;
        public bool wasRemoved;
    }

    [Serializable]
    public class RecordedEditorEvent
    {
        public int sequence;
        public float timeSinceStart;

        // "commit" for SomethingChanged / "undo" / "redo"
        public string eventKind;

        public int historyPosition;
        public int historyCount;

        public string source;
        public string changeType;
        public bool selectionOnly;

        public List<string> beforeSelectionUIDs = new();
        public List<string> afterSelectionUIDs = new();
        public List<RecordedSingleChange> changes = new();
    }

    [Serializable]
    public class RecordingSession
    {
        public int version = 1;
        public string sessionName;
        public float recordingStartRealtime;
        public DateTime savingTime;
        public int eventCount => events.Count;
        public TimeSpan duration;
        public v15LevelJSON levelStateAtStart;
        public List<RecordedEditorEvent> events = new();
        public List<CameraState> cameraStates = new();
    }

    public class CameraState
    {
        public float timeSinceStart;
        public Vector3 position;
        public Quaternion rotation;
    }

    internal class RecordManager
    {
        public static RecordManager Instance { get; } = new RecordManager();

        public bool IsRecording { get; private set; }
        public RecordingSession CurrentSession { get; private set; }

        private int _nextSequence;

        LEV_LevelEditorCentral central;

        public void StartRecording()
        {
            if (IsRecording)
            {
                Plugin.logger.LogWarning("[RecorderManager] StartRecording ignored, already recording.");
                return;
            }

            central = GameObject.FindObjectOfType<LEV_LevelEditorCentral>();

            if (central == null)
                return;

            CurrentSession = new RecordingSession
            {
                recordingStartRealtime = Time.realtimeSinceStartup,
                levelStateAtStart = central.saveload.ConvertCurrentLevelStateToJSON_v15()
            };

            Camera cam = central.cam.cameraCamera;

            if (cam != null && cam.GetComponent<CameraRecorder>() == null)
            {
                cam.gameObject.AddComponent<CameraRecorder>();
            }

            _nextSequence = 0;
            IsRecording = true;

            Plugin.logger.LogInfo("[RecorderManager] Recording started.");
        }

        public void StopRecording()
        {
            if (!IsRecording)
            {
                Plugin.logger.LogWarning("[RecorderManager] StopRecording ignored, not currently recording.");
                return;
            }

            IsRecording = false;
            Plugin.logger.LogInfo($"[RecorderManager] Recording stopped. Captured {CurrentSession?.events.Count ?? 0} events.");

            if (central != null)
            {
                Camera cam = central.cam.cameraCamera;

                CameraRecorder recorder = cam != null ? cam.GetComponent<CameraRecorder>() : null;

                if (recorder != null)
                {
                    GameObject.Destroy(recorder);
                }
                
            }

            try
            {
                CurrentSession.version = 1; // In case we want to bump version later and need to handle compatibility in loading.
                CurrentSession.sessionName = $"recording_{DateTime.Now:yyyyMMdd_HHmmss}";
                CurrentSession.savingTime = DateTime.Now;
                CurrentSession.duration = TimeSpan.FromSeconds(Time.realtimeSinceStartup - CurrentSession.recordingStartRealtime);
                FilesManager.FilesManager.SaveRecordingSession(Plugin.Storage, CurrentSession, $"recording_{DateTime.Now:yyyyMMdd_HHmmss}");

            }
            catch (Exception ex)
            {
                Plugin.logger.LogError($"[RecorderManager] Failed to save recording session: {ex}");
            }


        }

        public void CaptureSomethingChanged(LEV_UndoRedo undoRedo, Change_Collection whatChanged, string source)
        {
            if (!IsRecording)
                return;

            if (undoRedo == null)
            {
                Plugin.logger.LogWarning("[RecorderManager] CaptureSomethingChanged skipped: undoRedo was null.");
                return;
            }

            if (whatChanged == null)
            {
                Plugin.logger.LogWarning("[RecorderManager] CaptureSomethingChanged skipped: whatChanged was null.");
                return;
            }

            if (CurrentSession == null)
            {
                Plugin.logger.LogWarning("[RecorderManager] CaptureSomethingChanged skipped: no active session.");
                return;
            }

            RecordedEditorEvent evt = CreateRecordedEventFromChangeCollection(
                undoRedo,
                whatChanged,
                source,
                eventKind: "commit");

            CurrentSession.events.Add(evt);

            Plugin.logger.LogInfo($"[RecorderManager] Captured commit #{evt.sequence} type={evt.changeType} source={evt.source} changes={evt.changes.Count}");
        }

        private RecordedEditorEvent CreateRecordedEventFromChangeCollection(
            LEV_UndoRedo undoRedo,
            Change_Collection changeCollection,
            string fallbackSource,
            string eventKind)
        {
            var evt = new RecordedEditorEvent
            {
                sequence = _nextSequence++,
                timeSinceStart = Time.realtimeSinceStartup - CurrentSession.recordingStartRealtime,
                eventKind = eventKind,

                // In SomethingChanged postfix this represent the state after commit.
                historyPosition = undoRedo.currentHistoryPosition,
                historyCount = undoRedo.historyList != null ? undoRedo.historyList.Count : 0,

                source = !string.IsNullOrEmpty(changeCollection.source) ? changeCollection.source : fallbackSource,
                changeType = changeCollection.changeType.ToString(),
                selectionOnly = changeCollection.selectionOnly,

                beforeSelectionUIDs = changeCollection.beforeSelectionUIDs != null
                    ? new List<string>(changeCollection.beforeSelectionUIDs)
                    : new List<string>(),

                afterSelectionUIDs = changeCollection.afterSelectionUIDs != null
                    ? new List<string>(changeCollection.afterSelectionUIDs)
                    : new List<string>()
            };

            if (changeCollection.changeList != null)
            {
                foreach (Change_Single single in changeCollection.changeList)
                {
                    evt.changes.Add(CreateRecordedSingleChange(single));
                }
            }

            return evt;
        }

        private RecordedSingleChange CreateRecordedSingleChange(Change_Single single)
        {
            if (single == null)
            {
                return new RecordedSingleChange
                {
                    uid = string.Empty
                };
            }

            string uid = single.theBlockUID;

            if (string.IsNullOrEmpty(uid))
            {
                try
                {
                    uid = single.GetUID();
                }
                catch (Exception ex)
                {
                    Plugin.logger.LogWarning($"[RecorderManager] Failed to resolve UID from Change_Single: {ex}");
                    uid = string.Empty;
                }
            }

            bool wasAdded = false;
            bool wasRemoved = false;

            try
            {
                wasAdded = single.WasAdded();
                wasRemoved = single.WasRemoved();
            }
            catch (Exception ex)
            {
                Plugin.logger.LogWarning($"[RecorderManager] Failed to inspect add/remove state: {ex}");
            }

            return new RecordedSingleChange
            {
                uid = uid,

                beforeJson = single.before,
                afterJson = single.after,

                intBefore = single.int_before,
                intAfter = single.int_after,

                customSkyboxBefore = single.customSkybox_before,
                customSkyboxAfter = single.customSkybox_after,

                int2Before = single.int2_before,
                int2After = single.int2_after,

                boolBefore = single.bool_before,
                boolAfter = single.bool_after,

                changeBool1 = single.changeBool1,

                wasAdded = wasAdded,
                wasRemoved = wasRemoved
            };
        }

        private void CaptureHistoryTraversal(LEV_UndoRedo undoRedo, string eventKind)
        {
            if (!IsRecording)
                return;

            if (undoRedo == null)
            {
                Plugin.logger.LogWarning($"[RecorderManager] CaptureHistoryTraversal skipped: undoRedo was null for {eventKind}.");
                return;
            }

            if (CurrentSession == null)
            {
                Plugin.logger.LogWarning($"[RecorderManager] CaptureHistoryTraversal skipped: no active session for {eventKind}.");
                return;
            }

            if (undoRedo.currentHistoryPosition < 0 || undoRedo.currentHistoryPosition >= undoRedo.historyList.Count)
            {
                Plugin.logger.LogWarning($"[RecorderManager] CaptureHistoryTraversal skipped: history position out of range for {eventKind} ({undoRedo.currentHistoryPosition}/{undoRedo.historyList.Count}).");
                return;
            }

            Change_Collection changeCollection = undoRedo.historyList[undoRedo.currentHistoryPosition];
            if (changeCollection == null)
            {
                Plugin.logger.LogWarning($"[RecorderManager] CaptureHistoryTraversal skipped: target Change_Collection was null for {eventKind}.");
                return;
            }

            RecordedEditorEvent evt = CreateRecordedEventFromChangeCollection(
                undoRedo,
                changeCollection,
                fallbackSource: changeCollection.source,
                eventKind: eventKind);

            CurrentSession.events.Add(evt);

            Plugin.logger.LogInfo($"[RecorderManager] Captured {eventKind} #{evt.sequence} type={evt.changeType} source={evt.source} changes={evt.changes.Count}");
        }

        public void CaptureUndo(LEV_UndoRedo undoRedo)
        {
            CaptureHistoryTraversal(undoRedo, "undo");
        }

        public void CaptureRedo(LEV_UndoRedo undoRedo)
        {
            CaptureHistoryTraversal(undoRedo, "redo");
        }

        public void CaptureCameraState(Vector3 position, Quaternion rotation)
        {
            if (!IsRecording || CurrentSession == null)
                return;
            CurrentSession.cameraStates.Add(new CameraState
            {
                timeSinceStart = Time.realtimeSinceStartup - CurrentSession.recordingStartRealtime,
                position = position,
                rotation = rotation
            });
        }
    }
}
