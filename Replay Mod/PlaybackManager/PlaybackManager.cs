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

        public RecordingSession Session { get; private set; }

        private LEV_LevelEditorCentral central = null;

        public Dictionary<string, BlockProperties> allBlocksDictionary = new Dictionary<string, BlockProperties>();
        private float _timelinePlaybackStartRealtime;
        private float _timelinePlaybackStartEventTime;

        public float _currentSessionTime = 0f;

        private float _speedMultiplier = 1f;

        private SetupModelCar cameraManModel;

        public float SpeedMultiplier
        {
            get => _speedMultiplier;
            set
            {
                if (Mathf.Approximately(_speedMultiplier, value))
                    return;

                OnSpeedMultiplierChanged();
                _speedMultiplier = value;
            }
        }

        public void BeginPlayback(RecordingSession session)
        {
            if (session == null)
            {
                Plugin.logger.LogWarning("[PlaybackManager] BeginPlayback failed: session was null.");
                return;
            }

            central = GameObject.FindObjectOfType<LEV_LevelEditorCentral>();

            if (central == null)
            {
                Plugin.logger.LogWarning("[PlaybackManager] BeginPlayback failed: could not find LEV_LevelEditorCentral in the scene.");
                return;
            }

            Session = session;
            CurrentEventIndex = -1;
            IsPlaying = true;
            IsFollowingTimeline = false;
            _currentSessionTime = 0;
            _speedMultiplier = 1f;

            Plugin.logger.LogInfo($"[PlaybackManager] Started playback. Event count: {Session.events.Count}");

            LoadlevelStateAtStart();

            cameraManModel = CreateGhostCameraMan();

            if (cameraManModel == null)
            {
                Plugin.logger.LogError("Failed to create ghost camera man!");
                return;
            }

            cameraManModel.gameObject.SetActive(true);

            Plugin._guiDrawer.OpenPlaybackWindow();
        }

        private void LoadlevelStateAtStart()
        {
            if (Session == null)
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
                    Plugin.logger.LogError($"[PlaybackManager] Exception during level load: {ex}");
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
            central.manager.loader.levelJSON = Session.levelStateAtStart;
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
            Plugin.logger.LogInfo("[PlaybackManager] Stopped playback.");

            IsPlaying = false;
            IsFollowingTimeline = false;
            CurrentEventIndex = -1;
            Session = null;
            central = null;
            allBlocksDictionary.Clear();

            Plugin._guiDrawer.ClosePlaybackWindow();
        }

        public bool StepForward()
        {
            if (!IsPlaying)
            {
                Plugin.logger.LogWarning("[PlaybackManager] StepForward ignored: playback is not active.");
                return false;
            }

            if (Session == null)
            {
                Plugin.logger.LogWarning("[PlaybackManager] StepForward failed: session is null.");
                return false;
            }

            int nextIndex = CurrentEventIndex + 1;
            if (nextIndex >= Session.events.Count)
            {
                Plugin.logger.LogInfo("[PlaybackManager] Reached end of playback.");
                return false;
            }

            RecordedEditorEvent evt = Session.events[nextIndex];
            if (evt == null)
            {
                Plugin.logger.LogWarning($"[PlaybackManager] Event at index {nextIndex} was null.");
                CurrentEventIndex = nextIndex;
                return true;
            }

            try
            {
                ApplyEvent(evt);
                CurrentEventIndex = nextIndex;
                if (!IsFollowingTimeline)
                {
                    _currentSessionTime = GetEventTime(CurrentEventIndex);
                }
            }
            catch (Exception ex)
            {
                Plugin.logger.LogError($"[PlaybackManager] Failed to apply event at index {nextIndex}: {ex}");
                return false;
            }

            Plugin.logger.LogInfo($"[PlaybackManager] Applied event index={CurrentEventIndex} seq={evt.sequence} kind={evt.eventKind} type={evt.changeType}");
            return true;
        }

        public bool StepBackward()
        {
            if (!IsPlaying)
            {
                Plugin.logger.LogWarning("[PlaybackManager] StepBackward ignored: playback is not active.");
                return false;
            }

            if (Session == null)
            {
                Plugin.logger.LogWarning("[PlaybackManager] StepBackward failed: session is null.");
                return false;
            }

            if (CurrentEventIndex < 0)
            {
                Plugin.logger.LogInfo("[PlaybackManager] Already at the beginning.");
                return false;
            }

            RecordedEditorEvent evt = Session.events[CurrentEventIndex];
            if (evt == null)
            {
                Plugin.logger.LogWarning($"[PlaybackManager] Event at index {CurrentEventIndex} was null.");
                CurrentEventIndex--;
                return true;
            }

            try
            {
                ApplyEvent(evt, inverse: true);

                CurrentEventIndex--;
                if (!IsFollowingTimeline)
                {
                    _currentSessionTime = GetEventTime(CurrentEventIndex);
                }
            }
            catch (Exception ex)
            {
                Plugin.logger.LogError($"[PlaybackManager] Failed to revert event at index {CurrentEventIndex}: {ex}");
                return false;
            }

            return true;
        }

        public void StartFollowingTimeline()
        {
            if (!IsPlaying || Session == null)
            {
                Plugin.logger.LogWarning("[PlaybackManager] StartFollowingTimeline ignored: playback is not active.");
                return;
            }

            if (_currentSessionTime >= Session.duration.TotalSeconds)
            {
                Plugin.logger.LogInfo("[PlaybackManager] StartFollowingTimeline ignored: Playback reached the end.");
                return;
            }

            _timelinePlaybackStartRealtime = Time.realtimeSinceStartup;
            _timelinePlaybackStartEventTime = _currentSessionTime;

            IsFollowingTimeline = true;
            Plugin.logger.LogInfo("[PlaybackManager] Following timeline in realtime.");
        }

        public void StopFollowingTimeline()
        {
            if (!IsFollowingTimeline)
                return;

            float elapsedRealtime = Time.realtimeSinceStartup - _timelinePlaybackStartRealtime;
            _currentSessionTime = _timelinePlaybackStartEventTime + elapsedRealtime * _speedMultiplier;

            IsFollowingTimeline = false;
            Plugin.logger.LogInfo("[PlaybackManager] Realtime timeline playback paused.");
        }

        public void UpdateRealtimePlayback()
        {
            if (!IsPlaying || !IsFollowingTimeline || Session == null)
                return;

            float elapsedRealtime = Time.realtimeSinceStartup - _timelinePlaybackStartRealtime;
            float scaledElapsed = elapsedRealtime * _speedMultiplier;
            _currentSessionTime = (_timelinePlaybackStartEventTime + scaledElapsed);
            float targetSessionTime = _currentSessionTime;
            const float epsilon = 0.0001f;

            while (CurrentEventIndex + 1 < Session.events.Count)
            {
                RecordedEditorEvent nextEvent = Session.events[CurrentEventIndex + 1];
                if (nextEvent == null)
                {
                    int skippedIndex = CurrentEventIndex + 1;
                    Plugin.logger.LogWarning($"[PlaybackManager] Event at index {skippedIndex} was null during realtime follow. Skipping.");
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

            UpdateGhostFromTimeline(targetSessionTime);

            if (targetSessionTime >= Session.duration.TotalSeconds)
            {
                StopFollowingTimeline();
                Plugin.logger.LogInfo("[PlaybackManager] Realtime timeline playback reached the end.");
            }
        }

        private void ApplyEvent(RecordedEditorEvent evt, bool inverse = false)
        {
            bool applyBefore = ShouldApplyBefore(evt);

            if (inverse)
                applyBefore = !applyBefore;

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
                    Plugin.logger.LogWarning($"[PlaybackManager] Unknown change type: {evt.changeType}");
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
                Plugin.logger.LogInfo("[PlaybackManager] Block event had no changes.");
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
                    Plugin.logger.LogWarning("[PlaybackManager] Skipping block change with empty UID.");
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
                Plugin.logger.LogWarning("[PlaybackManager] Floor event had no change payload.");
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
                Plugin.logger.LogWarning("[PlaybackManager] Skybox event had no change payload.");
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
                Plugin.logger.LogError($"[PlaybackManager] RefreshConnectionsForAllBlocks encountered an issue: {ex}");
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
                Plugin.logger.LogWarning("[PlaybackManager] CreateBlockFromJson failed: newBlockValues is null.");
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
                Plugin.logger.LogError($"[PlaybackManager] Failed to create block from JSON for UID {uid}: {ex}");
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

        private void OnSpeedMultiplierChanged()
        {

            if (!IsFollowingTimeline)
                return;

            _currentSessionTime = _timelinePlaybackStartEventTime +
                (Time.realtimeSinceStartup - _timelinePlaybackStartRealtime) * _speedMultiplier;

            _timelinePlaybackStartEventTime = _currentSessionTime;
            _timelinePlaybackStartRealtime = Time.realtimeSinceStartup;
        }

        private float GetEventTime(int index)
        {
            return index >= 0 && index < Session.events.Count
                ? Session.events[index].timeSinceStart
                : 0f;
        }

        public void ScrubToTime(float targetTime)
        {
            StopFollowingTimeline();

            int targetIndex = FindEventIndexForTime(targetTime);

            if (targetIndex > CurrentEventIndex)
            {
                // Step forward
                while (CurrentEventIndex < targetIndex)
                {
                    if (!StepForward())
                        break;
                }
            }
            else if (targetIndex < CurrentEventIndex)
            {
                // Step backward
                while (CurrentEventIndex > targetIndex)
                {
                    if (!StepBackward())
                        break;
                }
            }

            UpdateGhostFromTimeline(targetTime);

            _currentSessionTime = targetTime;
        }

        private int FindEventIndexForTime(float targetTime)
        {
            int left = 0;
            int right = Session.events.Count - 1;
            int result = -1;

            while (left <= right)
            {
                int mid = (left + right) / 2;
                var evt = Session.events[mid];

                if (evt == null)
                {
                    int temp = mid;
                    while (temp >= left && Session.events[temp] == null)
                        temp--;

                    if (temp < left)
                    {
                        left = mid + 1;
                        continue;
                    }

                    mid = temp;
                    evt = Session.events[mid];
                }

                if (evt.timeSinceStart <= targetTime)
                {
                    result = mid;
                    left = mid + 1;
                }
                else
                {
                    right = mid - 1;
                }
            }

            return result;
        }

        public SetupModelCar CreateGhostCameraMan()
        {
            var spawner = GameObject.FindObjectOfType<NetworkedGhostSpawner>();

            if (spawner == null)
            {
                Plugin.logger.LogError("NetworkedGhostSpawner not found!");
                return null;
            }

            // Instantiate camera man
            var cameraManObj = GameObject.Instantiate(
                spawner.zeepkistGhostPrefab.cameraManModel.gameObject
            );

            cameraManObj.name = "GhostCameraMan";

            var cameraMan = cameraManObj.GetComponent<SetupModelCar>();

            var cam = cameraMan.transform.Find("Character/Right Arm/Camera");
            if (cam != null)
            {
                cam.gameObject.SetActive(false);
            }

            PlayerManager playerManager = GameObject.FindObjectOfType<PlayerManager>();

            if (playerManager == null)
            {
                Plugin.logger.LogError("PlayerManager not found!");
                return null;
            }

            CosmeticsV16 ghostCosmetics = new CosmeticsV16();
            if (playerManager.adventureCosmetics != null)
            {
                ghostCosmetics.IDsToCosmetics(playerManager.adventureCosmetics.GetIDs());
            }
            cameraMan.DoCarSetup(ghostCosmetics, false, false, false);

            if (Session.cameraStates.Count > 0)
            {
                cameraMan.transform.position = Session.cameraStates[0].position;
                cameraMan.transform.rotation = Session.cameraStates[0].rotation;
            }
            

            return cameraMan;
        }

        private void GetSurroundingStates(float time, out CameraState a, out CameraState b)
        {
            a = null;
            b = null;

            for (int i = 0; i < Session.cameraStates.Count - 1; i++)
            {
                var current = Session.cameraStates[i];
                var next = Session.cameraStates[i + 1];

                if (current.timeSinceStart <= time && next.timeSinceStart >= time)
                {
                    a = current;
                    b = next;
                    return;
                }
            }

            // Edge cases
            if (Session.cameraStates.Count > 0)
            {
                a = Session.cameraStates[0];
                b = Session.cameraStates[Session.cameraStates.Count - 1];
            }
        }

        public void UpdateGhostFromTimeline(float time)
        {
            if (cameraManModel == null || Session.cameraStates.Count == 0)
                return;

            GetSurroundingStates(time, out var a, out var b);

            if (a == null || b == null)
                return;

            float t;

            if (Mathf.Abs(b.timeSinceStart - a.timeSinceStart) < 0.0001f)
            {
                t = 0f;
            }
            else
            {
                t = Mathf.InverseLerp(a.timeSinceStart, b.timeSinceStart, time);
            }

            Vector3 pos = Vector3.Lerp(a.position, b.position, t);
            Quaternion rot = Quaternion.Slerp(a.rotation, b.rotation,t);

            cameraManModel.transform.position = pos;
            cameraManModel.transform.rotation = rot;
        }
    }
}
