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
        public int CurrentEventIndex { get; private set; } = -1;

        private RecordingSession _session;
        private LEV_UndoRedo _undoRedo;

        public void BeginPlayback(RecordingSession session, LEV_UndoRedo undoRedo)
        {
            if (session == null)
            {
                Plugin.logger.LogWarning("[EditorRecorder] BeginPlayback failed: session was null.");
                return;
            }

            if (undoRedo == null)
            {
                Plugin.logger.LogWarning("[EditorRecorder] BeginPlayback failed: undoRedo was null.");
                return;
            }

            _session = session;
            _undoRedo = undoRedo;
            CurrentEventIndex = -1;
            IsPlaying = true;

            Plugin.logger.LogInfo($"[EditorRecorder] Started playback. Event count: {_session.events.Count}");
        }

        public void StopPlayback()
        {
            Plugin.logger.LogInfo("[EditorRecorder] Stopped playback.");

            IsPlaying = false;
            CurrentEventIndex = -1;
            _session = null;
            _undoRedo = null;
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

        public void ResetToCleanEditor()
        {
            if (_undoRedo == null)
            {
                Plugin.logger.LogWarning("[EditorRecorder] ResetToCleanEditor failed: undoRedo is null.");
                return;
            }


                RecordManager.RecordManager.Instance.SuppressCapture = true;

                _undoRedo.AddAllBlocksToDictionary();

                List<string> allUids = new List<string>(_undoRedo.allBlocksDictionary.Keys);
                for (int i = 0; i < allUids.Count; i++)
                {
                    string uid = allUids[i];
                    BlockProperties block = TryGetLiveBlock(uid);
                    if (block != null)
                    {
                        _undoRedo.allBlocksDictionary.Remove(uid);
                        UnityEngine.Object.Destroy(block.gameObject);
                    }
                }

                _undoRedo.central.selection.DeselectAllBlocks(false, "PlaybackReset");
                _undoRedo.AddAllBlocksToDictionary();

                Plugin.logger.LogInfo("[EditorRecorder] Reset editor to clean state.");

                RecordManager.RecordManager.Instance.SuppressCapture = false;
        }

        private void ApplyEvent(RecordedEditorEvent evt)
        {
            bool applyBefore = ShouldApplyBefore(evt);

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

            HashSet<string> recreatedUids = new HashSet<string>();

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
                if (newBlock != null)
                {
                    recreatedUids.Add(uid);
                }
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

            _undoRedo.central.painter.SetLoadGroundMaterial(materialIndex);
            Reselect(evt, applyBefore);
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
                _undoRedo.central.skybox.simulateLofi = change.boolBefore;
                _undoRedo.central.skybox.SetToSkybox(change.intBefore, true, customSkybox, true, false);
                _undoRedo.central.skyboxTool.SetCurrentPage(change.int2Before);
            }
            else
            {
                _undoRedo.central.skybox.simulateLofi = change.boolAfter;
                _undoRedo.central.skybox.SetToSkybox(change.intAfter, true, customSkybox, true, false);
                _undoRedo.central.skyboxTool.SetCurrentPage(change.int2After);
            }

            _undoRedo.central.skyboxTool.ReloadInternalSettings();
            _undoRedo.central.skyboxTool.DrawOptionPanels();

            Reselect(evt, applyBefore);
        }

        private void ApplySelectionEvent(RecordedEditorEvent evt, bool applyBefore)
        {
            Reselect(evt, applyBefore);
        }

        private void Reselect(RecordedEditorEvent evt, bool applyBefore)
        {
            if (_undoRedo == null || evt == null)
                return;

            List<string> targetUids = applyBefore
                ? evt.beforeSelectionUIDs
                : evt.afterSelectionUIDs;

            if (targetUids == null)
                targetUids = new List<string>();

            List<BlockProperties> blocks = new List<BlockProperties>();

            for (int i = 0; i < targetUids.Count; i++)
            {
                string uid = targetUids[i];
                BlockProperties block = TryGetLiveBlock(uid);
                if (block != null)
                {
                    blocks.Add(block);
                }
            }

            if (blocks.Count > 0 && _undoRedo.central.tool.currentTool != 0)
            {
                _undoRedo.central.tool.EnableEditTool();
            }

            _undoRedo.central.selection.UndoRedoReselection(blocks);
        }

        private BlockProperties TryGetLiveBlock(string uid)
        {
            if (_undoRedo == null || string.IsNullOrEmpty(uid))
                return null;

            if (_undoRedo.allBlocksDictionary.TryGetValue(uid, out BlockProperties block))
                return block;

            return null;
        }

        private void DestroyExistingBlock(string uid)
        {
            BlockProperties existing = TryGetLiveBlock(uid);
            if (existing == null)
                return;

            _undoRedo.allBlocksDictionary.Remove(uid);
            UnityEngine.Object.Destroy(existing.gameObject);
        }

        private BlockProperties CreateBlockFromJson(BlockPropertyJSON newBlockValues, string uid)
        {
            if (_undoRedo == null)
            {
                Plugin.logger.LogWarning("[EditorRecorder] CreateBlockFromJson failed: undoRedo is null.");
                return null;
            }

            if (newBlockValues == null)
            {
                Plugin.logger.LogWarning("[EditorRecorder] CreateBlockFromJson failed: newBlockValues is null.");
                return null;
            }

            try
            {
                BlockProperties prefab = _undoRedo.central.manager.loader.globalBlockList.blocks[newBlockValues.i];
                BlockProperties block = UnityEngine.Object.Instantiate(prefab);

                block.gameObject.name = prefab.gameObject.name;
                block.CreateBlock();
                block.DrawDebugUID();
                block.properties.Clear();
                block.isEditor = true;
                block.UID = uid;
                block.LoadProperties_v15(newBlockValues, false);
                block.isLoading = false;

                _undoRedo.AddBlockToDictionary(uid, block);
                StaticConnectorTracker.UpdateBlockUIDInTracker(block.UID, block);

                return block;
            }
            catch (Exception ex)
            {
                Plugin.logger.LogError($"[EditorRecorder] Failed to create block from JSON for UID {uid}: {ex}");
                return null;
            }
        }

        private void RefreshConnectionsForAllBlocks()
        {
            if (_undoRedo == null)
                return;

            try
            {
                _undoRedo.AddAllBlocksToDictionary();

                foreach (KeyValuePair<string, BlockProperties> kvp in _undoRedo.allBlocksDictionary)
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
                Plugin.logger.LogWarning($"[EditorRecorder] RefreshConnectionsForAllBlocks encountered an issue: {ex}");
            }
        }
    }
}