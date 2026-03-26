using Replay_Mod;
using ReplayMod.RecordManager;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ReplayMod.PlaybackManager
{

    public class PlaybackManager
    {
        public static PlaybackManager Instance { get; } = new PlaybackManager();

        public bool IsPlaying { get; private set; }
        public bool IsFollowingTimeline { get; private set; }
        public int CurrentEventIndex { get; private set; } = -1;

        private RecordingSession _session;

        private LEV_LevelEditorCentral central = null;

        public Dictionary<string, BlockProperties> allBlocksDictionary = new Dictionary<string, BlockProperties>();
        private float _timelinePlaybackStartRealtime;
        private float _timelinePlaybackStartEventTime;

        public void BeginPlayback(RecordingSession session)
        {
            if (session == null)
            {
                Plugin.logger.LogWarning("[EditorRecorder] BeginPlayback failed: session was null.");
                return;
            }

            central = GameObject.FindObjectOfType<LEV_LevelEditorCentral>();

            if (central == null)
            {
                Plugin.logger.LogWarning("[EditorRecorder] BeginPlayback failed: could not find LEV_LevelEditorCentral in the scene.");
                return;
            }

            _session = session;
            CurrentEventIndex = -1;
            IsPlaying = true;
            IsFollowingTimeline = false;

            Plugin.logger.LogInfo($"[EditorRecorder] Started playback. Event count: {_session.events.Count}");

            LoadlevelStateAtStart();
        }

        private void LoadlevelStateAtStart()
        {
            if (_session == null)
                return;

            PrimeGeneralLevelLoader();
            bool isLoadSuccessful = false;
            while (!isLoadSuccessful)
            {
                try
                {
                    isLoadSuccessful = Loadlevel();
                }
                catch (Exception ex)
                {
                    Plugin.logger.LogError($"[EditorRecorder] Exception during level load: {ex}");
                    isLoadSuccessful = false;
                }
            }
           
            PopulateAllBlocksDictionary();
        }
        private void PrimeGeneralLevelLoader()
        {
            central.manager.loader.useV15loading = true;
            central.manager.loader.PrimeGeneric("PrimeForLevelEditor()_v15");
            central.manager.loader.loadType = GeneralLevelLoader.GameEditorSwitch.editor;
            central.manager.loader.skybox = central.saveload.skybox;
            central.manager.loader.levelJSON = _session.levelStateAtStart;
            central.manager.loader.newCentral = central;

            central.manager.loader.levelJSON.editcam.euler = new CV3(central.cam.cameraTransform.eulerAngles);
            central.manager.loader.levelJSON.editcam.pos = new CV3(central.cam.transform.position);
            central.manager.loader.levelJSON.editcam.rotXY = new CV2(new Vector2(central.cam.rotationX, central.cam.rotationY));

        }

        private bool Loadlevel()
        {
            return central.manager.loader.DoLoad_v15();
        }

        private void PopulateAllBlocksDictionary()
        {
            List<BlockProperties> existingBlocks = central.saveload.GetAllBlockPropertiesCurrentlyInLevel();


            foreach (BlockProperties existingBlock in existingBlocks)
            {
                if (existingBlock == null)
                    continue;
                AddBlockToDictionary(existingBlock.UID, existingBlock);
            }
        }

        public void StopPlayback()
        {
            Plugin.logger.LogInfo("[EditorRecorder] Stopped playback.");

            IsPlaying = false;
            IsFollowingTimeline = false;
            CurrentEventIndex = -1;
            _session = null;
            central = null;
            allBlocksDictionary.Clear();
        }

        public bool StepForward()
        {
            if (!IsPlaying)
            {
                Plugin.logger.LogWarning("[EditorRecorder] StepForward ignored: playback is not active.");
                return false;
            }

            if (_session == null)
            {
                Plugin.logger.LogWarning("[EditorRecorder] StepForward failed: session is null.");
                return false;
            }

            int nextIndex = CurrentEventIndex + 1;
            if (nextIndex >= _session.events.Count)
            {
                Plugin.logger.LogInfo("[EditorRecorder] Reached end of playback.");
                return false;
            }

            RecordedEditorEvent evt = _session.events[nextIndex];
            if (evt == null)
            {
                Plugin.logger.LogWarning($"[EditorRecorder] Event at index {nextIndex} was null.");
                CurrentEventIndex = nextIndex;
                return true;
            }

            try
            {
                RecordManager.RecordManager.Instance.SuppressCapture = true;
                ApplyEvent(evt);
                CurrentEventIndex = nextIndex;
            }
            catch (Exception ex)
            {
                Plugin.logger.LogError($"[EditorRecorder] Failed to apply event at index {nextIndex}: {ex}");
                return false;
            }

            RecordManager.RecordManager.Instance.SuppressCapture = false;

            Plugin.logger.LogInfo($"[EditorRecorder] Applied event index={CurrentEventIndex} seq={evt.sequence} kind={evt.eventKind} type={evt.changeType}");
            return true;
        }

        public void StartFollowingTimeline()
        {
            if (!IsPlaying || _session == null)
            {
                Plugin.logger.LogWarning("[EditorRecorder] StartFollowingTimeline ignored: playback is not active.");
                return;
            }

            if (CurrentEventIndex + 1 >= _session.events.Count)
            {
                Plugin.logger.LogInfo("[EditorRecorder] StartFollowingTimeline ignored: no remaining events.");
                return;
            }

            _timelinePlaybackStartRealtime = Time.realtimeSinceStartup;
            _timelinePlaybackStartEventTime = CurrentEventIndex >= 0
                ? _session.events[CurrentEventIndex].timeSinceStart
                : 0f;

            IsFollowingTimeline = true;
            Plugin.logger.LogInfo("[EditorRecorder] Following timeline in realtime.");
        }

        public void StopFollowingTimeline()
        {
            if (!IsFollowingTimeline)
                return;

            IsFollowingTimeline = false;
            Plugin.logger.LogInfo("[EditorRecorder] Realtime timeline playback paused.");
        }

        public void UpdateRealtimePlayback()
        {
            if (!IsPlaying || !IsFollowingTimeline || _session == null)
                return;

            float elapsedRealtime = Time.realtimeSinceStartup - _timelinePlaybackStartRealtime;
            float targetSessionTime = _timelinePlaybackStartEventTime + elapsedRealtime;
            const float epsilon = 0.0001f;

            while (CurrentEventIndex + 1 < _session.events.Count)
            {
                RecordedEditorEvent nextEvent = _session.events[CurrentEventIndex + 1];
                if (nextEvent == null)
                {
                    int skippedIndex = CurrentEventIndex + 1;
                    Plugin.logger.LogWarning($"[EditorRecorder] Event at index {skippedIndex} was null during realtime follow. Skipping.");
                    CurrentEventIndex = skippedIndex;
                    continue;
                }

                if (nextEvent.timeSinceStart > targetSessionTime + epsilon)
                    break;

                if (!StepForward())
                {
                    StopFollowingTimeline();
                    return;
                }
            }

            if (CurrentEventIndex + 1 >= _session.events.Count)
            {
                StopFollowingTimeline();
                Plugin.logger.LogInfo("[EditorRecorder] Realtime timeline playback reached the end.");
            }
        }

        public void ResetToCleanEditor()
        {

            CurrentEventIndex = -1;
            IsFollowingTimeline = false;
            allBlocksDictionary.Clear();
        }

        private void ApplyEvent(RecordedEditorEvent evt)
        {
            bool applyBefore = ShouldApplyBefore(evt);
            central.selection.DeselectAllBlocks(false, "[ReplayMod]Playback");
            switch (evt.changeType)
            {
                case "block":
                    ApplyBlockEvent(evt, applyBefore);
                    break;

                case "connection":
                    ApplyConnectionEvent(evt, applyBefore);
                    break;

                case "floor":
                    ApplyFloorEvent(evt, applyBefore);
                    break;

                case "skybox":
                    ApplySkyboxEvent(evt, applyBefore);
                    break;

                case "selection":
                    ApplySelectionEvent(evt, applyBefore);
                    break;

                default:
                    Plugin.logger.LogWarning($"[EditorRecorder] Unknown change type: {evt.changeType}");
                    break;
            }
        }

        private bool ShouldApplyBefore(RecordedEditorEvent evt)
        {
            return evt.eventKind == "undo";
        }

        private void ApplyBlockEvent(RecordedEditorEvent evt, bool applyBefore)
        {
            if (evt.changes == null || evt.changes.Count == 0)
            {
                Plugin.logger.LogInfo("[EditorRecorder] Block event had no changes.");
                return;
            }


            foreach (RecordedSingleChange change in evt.changes)
            {
                if (change == null)
                    continue;

                string uid = change.uid;
                string targetJson = applyBefore ? change.beforeJson : change.afterJson;

                if (string.IsNullOrEmpty(uid))
                {
                    Plugin.logger.LogWarning("[EditorRecorder] Skipping block change with empty UID.");
                    continue;
                }

                DestroyExistingBlock(uid);

                if (string.IsNullOrEmpty(targetJson))
                {
                    continue;
                }

                BlockPropertyJSON blockJson = LEV_UndoRedo.GetJSONblock(targetJson);
                BlockProperties newBlock = CreateBlockFromJson(blockJson, uid);
            }

            RefreshConnectionsForAllBlocks();
            Reselect(evt, applyBefore);
        }

        private void ApplyConnectionEvent(RecordedEditorEvent evt, bool applyBefore)
        {
            ApplyBlockEvent(evt, applyBefore);
        }

        private void ApplyFloorEvent(RecordedEditorEvent evt, bool applyBefore)
        {
            if (evt.changes == null || evt.changes.Count == 0)
            {
                Plugin.logger.LogWarning("[EditorRecorder] Floor event had no change payload.");
                return;
            }

            RecordedSingleChange change = evt.changes[0];
            int materialIndex = applyBefore ? change.intBefore : change.intAfter;

            central.painter.SetLoadGroundMaterial(materialIndex);
        }

        private void ApplySkyboxEvent(RecordedEditorEvent evt, bool applyBefore)
        {
            if (evt.changes == null || evt.changes.Count == 0)
            {
                Plugin.logger.LogWarning("[EditorRecorder] Skybox event had no change payload.");
                return;
            }

            RecordedSingleChange change = evt.changes[0];

            string customSkyboxJson = applyBefore
                ? change.customSkyboxBefore
                : change.customSkyboxAfter;

            SkyboxCreator_DataObject customSkybox = null;
            if (!string.IsNullOrEmpty(customSkyboxJson))
            {
                customSkybox = CustomSkyboxHelpers.ConvertFromJSON(customSkyboxJson);
            }

            if (applyBefore)
            {
                central.skybox.simulateLofi = change.boolBefore;
                central.skybox.SetToSkybox(change.intBefore, true, customSkybox, true, false);
            }
            else
            {
                central.skybox.simulateLofi = change.boolAfter;
                central.skybox.SetToSkybox(change.intAfter, true, customSkybox, true, false);
            }
        }

        private void ApplySelectionEvent(RecordedEditorEvent evt, bool applyBefore)
        {
            Reselect(evt, applyBefore);
        }

        private void RefreshConnectionsForAllBlocks()
        {
            try
            {
                foreach (KeyValuePair<string, BlockProperties> kvp in allBlocksDictionary)
                {
                    BlockProperties block = kvp.Value;
                    if (block == null)
                        continue;

                    block.LoadOnlyPropertyScripts();

                    List<BlockEdit_v18_Connector_Base> connectors =
                        StaticConnectorTracker.GetAllBlockEditV18ConnectorsOnThisBlock(kvp.Key);

                    if (connectors == null)
                        continue;

                    for (int i = 0; i < connectors.Count; i++)
                    {
                        connectors[i].ForceRedrawConnectionVisualizers();
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.logger.LogError($"[EditorRecorder] RefreshConnectionsForAllBlocks encountered an issue: {ex}");
            }
        }

        private void Reselect(RecordedEditorEvent evt, bool applyBefore)
        {
            if (evt == null)
                return;
            List<string> targetUids;
            List<string> selectedBlocks;
            if (applyBefore)
            {
                targetUids = evt.beforeSelectionUIDs;
                selectedBlocks = evt.afterSelectionUIDs;
            }
            else
            {
                targetUids = evt.afterSelectionUIDs;
                selectedBlocks = evt.beforeSelectionUIDs;
            }

            targetUids ??= [];
            selectedBlocks ??= [];

            for (int i = 0; i < selectedBlocks.Count; i++)
            {
                string uid = selectedBlocks[i];
                BlockProperties block = TryGetLiveBlock(uid);
                if (block != null)
                {
                    central.selection.RestorePaint(block);
                }
            }


            for (int i = 0; i < targetUids.Count; i++)
            {
                string uid = targetUids[i];
                BlockProperties block = TryGetLiveBlock(uid);
                if (block != null)
                {
                    central.selection.SelectionPaint(block);
                }
            }
        }

        private BlockProperties TryGetLiveBlock(string uid)
        {
            if (string.IsNullOrEmpty(uid))
                return null;

            if (allBlocksDictionary.TryGetValue(uid, out BlockProperties block))
                return block;

            return null;
        }

        private void DestroyExistingBlock(string uid)
        {
            BlockProperties existing = TryGetLiveBlock(uid);
            if (existing == null)
                return;

            allBlocksDictionary.Remove(uid);
            UnityEngine.Object.Destroy(existing.gameObject);
        }

        private BlockProperties CreateBlockFromJson(BlockPropertyJSON newBlockValues, string uid)
        {
            if (newBlockValues == null)
            {
                Plugin.logger.LogWarning("[EditorRecorder] CreateBlockFromJson failed: newBlockValues is null.");
                return null;
            }

            try
            {
                BlockProperties prefab = central.manager.loader.globalBlockList.blocks[newBlockValues.i];
                BlockProperties block = UnityEngine.Object.Instantiate(prefab);

                block.gameObject.name = prefab.gameObject.name;
                block.CreateBlock();
                block.properties.Clear();
                block.isEditor = true;
                block.UID = uid;
                block.LoadProperties_v15(newBlockValues, false);
                block.isLoading = false;

                AddBlockToDictionary(uid, block);
                StaticConnectorTracker.UpdateBlockUIDInTracker(block.UID, block);

                return block;
            }
            catch (Exception ex)
            {
                Plugin.logger.LogError($"[EditorRecorder] Failed to create block from JSON for UID {uid}: {ex}");
                return null;
            }
        }

        public void AddBlockToDictionary(string uid, BlockProperties theNewBlock)
        {
            if (this.allBlocksDictionary.ContainsKey(uid))
            {
                this.allBlocksDictionary[uid] = theNewBlock;
                return;
            }
            this.allBlocksDictionary.Add(uid, theNewBlock);
        }
    }
}
