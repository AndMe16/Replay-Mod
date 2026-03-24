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
        public float recordingStartRealtime;
        public List<RecordedEditorEvent> events = new();
    }

    internal class RecordManager
    {
        public static RecordManager Instance { get; } = new RecordManager();

        public bool IsRecording { get; private set; }
        public RecordingSession CurrentSession { get; private set; }

        private int _nextSequence;

        public void StartRecording()
        {
            if(IsRecording)
            {
                Plugin.logger.LogWarning("[EditorRecorder] StartRecording ignored, already recording.");
                return;
            }


            CurrentSession = new RecordingSession
            {
                recordingStartRealtime = Time.realtimeSinceStartup
            };

            _nextSequence = 0;
            IsRecording = true;

            Plugin.logger.LogInfo("[EditorRecorder] Recording started.");
        }

        public void StopRecording()
        {
            if (!IsRecording)
            {
                Plugin.logger.LogWarning("[EditorRecorder] StopRecording ignored, not currently recording.");
                return;
            }

            IsRecording = false;
            Plugin.logger.LogInfo($"[EditorRecorder] Recording stopped. Captured {CurrentSession?.events.Count ?? 0} events.");
        }

        public void CaptureSomethingChanged(LEV_UndoRedo undoRedo, Change_Collection whatChanged, string source)
        {
            if (!IsRecording)
                return;

            if (undoRedo == null)
            {
                Plugin.logger.LogWarning("[EditorRecorder] CaptureSomethingChanged skipped: undoRedo was null.");
                return;
            }

            if (whatChanged == null)
            {
                Plugin.logger.LogWarning("[EditorRecorder] CaptureSomethingChanged skipped: whatChanged was null.");
                return;
            }

            if (CurrentSession == null)
            {
                Plugin.logger.LogWarning("[EditorRecorder] CaptureSomethingChanged skipped: no active session.");
                return;
            }

            RecordedEditorEvent evt = CreateRecordedEventFromChangeCollection(
                undoRedo,
                whatChanged,
                source,
                eventKind: "commit");

            CurrentSession.events.Add(evt);

            Plugin.logger.LogInfo($"[EditorRecorder] Captured commit #{evt.sequence} type={evt.changeType} source={evt.source} changes={evt.changes.Count}");
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
                    Plugin.logger.LogWarning($"[EditorRecorder] Failed to resolve UID from Change_Single: {ex}");
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
                Plugin.logger.LogWarning($"[EditorRecorder] Failed to inspect add/remove state: {ex}");
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
    }
}
