extern alias wle;
using FG.Common;
using FG.Common.Character;
using FG.Common.LODs;
using FG.Common.Network;
using FGClient;
using FGTools.Internal;
using FGTools.Internal.Behaviours;
using FGTools.Services;
using FGTools.States.Logic;
using HarmonyLib;
using Levels;
using Levels.DoorDash;
using Levels.Obstacles;
using Levels.Progression;
using Levels.ScoreZone;
using Levels.TipToe;
using Levels.WallGuys;
using SRF;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace FGTools.LocalServer.Patches
{
    /// <summary>
    /// Patches that should run only on server side
    /// </summary>
    internal class ServerGameplayPatches : FGTBase
    {
        //list of classes that should think they're running on server side (wait, they ACTUALLY running on server side :rofl:)
        public static readonly HashSet<string> IsGameServerList =
        [
            "WallGuysSegmentGenerator",
            "COMMON_Button",
            "RMIBehaviour",
            "ScoredButton",
            "COMMON_Wormhole",
            "MotorFunctionPortalStateActive",
            "COMMON_SeeSaw360",
            "COMMON_PrefabSpawnerBase",
            "MPGNetObject",
            "COMMON_PrefabSpawnerTimed",
            "MPGNetObjectBootstrapper",
            "OnTriggerLevelEventEmitter",
            "OnWaterBalloonTriggerLevelEventEmitter",
            "LevelEventManager",
            "COMMON_PrefabSpawnerTriggered",
            "ServerOnlyComponent",
            "BlastBallManager",
            "COMMON_BlastBall",
            "COMMON_SpawnBasket",
            "SelfRespawnerFX",
            "COMMON_SelfRespawner",
            "ExplosionEventHandler",
            "BlastBallExplosionEventHandler",
            "ScoreAwarder",
            "ChickenChaseController",
            "CircuitManager",
            "COMMON_SnowMound",
            "VolleyfallScoreController",
            "COMMON_TriggerVolume",
            "BubbleZoneTrigger",
            "CollectableTrigger",
            "COMMON_BreakableIceTile",
            "ChickenAI",
            "NPCAI",
            "BullAI",
            "NPCController",
            "COMMON_BullRMIController",
            "MPGNetObjectBase",
            "ScoreZoneManager",
            "VolumeZoneTrigger",
            "KrakenTentacleController",
            "KrakenAttackManager",
            "KrakenAttackRaft",
            "KrakenAttackRetractableTile",
            "KrakenAttackTile",
            "JumpShowdown_Platform",
            "TeamQualificationObjectsScoreTracker",
            "TerritoryControl_Tile",
            "BubbleZone",
            "CollectionZone",
            "PixelPerfectInputTileTrigger",
            "PixelPerfectBoard",
            "SnowyScrapManager",
            "COMMON_Scaleable",
            "COMMON_SnowMound",
            "JumpShowdown_PlatformsController",
            "COMMON_TimeAttackTrigger",
            "TimeAttackItemTrigger",
            "COMMON_SnowballSurvivalBall",
            "TipToe_Platform",
            "RMIBehaviourManager",
            "COMMON_GrabToQualify",
            "LevelEditorCommonPrefabSpawnerBase",
            "LevelEditorCommonPrefabSpawnerTimed",
            "COMMON_ScoringBubbleTrigger",
            "LevelEditorTriggerZoneActiveBase",
            "LevelEditorTriggerScoreFeedback",
            "LevelEditorScoringFrequencyParameter",
            "COMMON_ScoringBubble",
            "COMMON_ScoringBubbleLevelEditorZoneService",
            "LevelEditorBubbleHandler",
            "LevelEditorObjectCollider",
            "LevelEditorSimpleDrawable",
            "LevelEditorPointsScoredParameter",
            "LevelEditorActiveObjectBase",
            "COMMON_PowerupPickup",
            "CheckpointManager",
            "TimeAttackManager",
            "COMMON_KillZone",
            "COMMON_ExplodingRhino",
            "ExplodingRhinoAI",
            "LevelEditorCommonButton",
            "LevelEditorCommonFlipper",
            "COMMON_BounceBoard",
            "PressurePlate",
            "ExplodingRhinoRMIManager",
            "COMMON_ObjectiveReachEndZone",
            "COMMON_PlayerEliminationVolume",
            "TimeAttackPlayerStats",
            "COMMON_InfiniteSegmentSpawner",
            "VolumeZone",
            "COMMON_FakeDoorRandomiser",
            "MPGNetObjectPossessable",
            "COMMON_GridPathRandomiser",
            "TipToe_PlatformShakeController",
            "JumbotronController",
            "FloorFall_FloorController",
            "COMMON_RespawningTile",
            "LevelEditorSlimeKillZone",
            "LevelEditorLavaController",
            "LevelEditorCheckpointManager",
            "LevelEditorCheckpointZone",
            "LevelEditorSlimeVolume",
            "COMMON_Forcefield",
            "COMMON_Sequence",
            //"MotorAgent", //causes crash, no clue why
            "COMMON_TimedPivotable",
            "CollectableZoneTrigger",
            "LodController",
            "LodManager",
            "COMMON_ObjectiveBase",
            "COMMON_ScaleWhileMoving",
            "FollowTheLeaderZone",
            "AIPath",
            "ChickenController",
            "ChickenChaseController"
        ];


        [HarmonyPatch(typeof(ClientGameStateView), nameof(ClientGameStateView.IsGameServer), MethodType.Getter), HarmonyPostfix]
        static void IsGameServer(ClientGameStateView __instance, ref bool __result)
        {
            __result = /*LocalServerService.IsServerInOperation && GlobalGameStateClient.Instance.GameStateView.IsGamePlaying*/ false;
        }

        [HarmonyPatch(typeof(FGBehaviour), nameof(FG.Common.FGBehaviour.GameState), MethodType.Getter), HarmonyPostfix]
        static void GameState(FGBehaviour __instance, ref IGameStateView __result)
        {
            if (LocalServerService.IsServerInOperation && IsGameServerList.Contains(__instance.GetIl2CppType().Name) && LocalServerService.GameStateView != null)
                __result = LocalServerService.GameStateView;
        }

        [HarmonyPatch(typeof(ClientGameStateView), nameof(ClientGameStateView.IsComboServer), MethodType.Getter), HarmonyPostfix]
        static void IsComboServer(ClientGameStateView __instance, ref bool __result)
        {
            __result = LocalServerService.IsServerInOperation;
        }

        [HarmonyPatch(typeof(wle.LevelEditorBubbleHandler), nameof(wle.LevelEditorBubbleHandler.IsInExploreOrPlayState), MethodType.Getter), HarmonyPrefix]
        static bool IsInExploreOrPlayState(wle.LevelEditorBubbleHandler __instance, ref bool __result)
        {
            __result = LocalServerService.IsServerInOperation && GlobalGameStateClient.Instance.GameStateView.IsGamePlaying;
            return false;
        }

        [HarmonyPatch(typeof(COMMON_TriggerVolume), nameof(COMMON_TriggerVolume.ConsiderEnterSense)), HarmonyPrefix]
        static bool ConsiderEnterSense(COMMON_TriggerVolume __instance, GameObject other)
        {
            if (other == null)
                return true;

            if (__instance.GetIl2CppType().Name == typeof(COMMON_ScoringBubbleTrigger).Name && __instance.CheckIsValid(other, out var obj) && obj.IsFallGuy)
                ServerGameStateActions.Instance.AwardPoints(obj, __instance.Cast<COMMON_ScoringBubbleTrigger>()._bubble._pointsAwarded);

            return true;
        }

        [HarmonyPatch(typeof(wle.LevelEditorTriggerZoneActiveBase), nameof(wle.LevelEditorTriggerZoneActiveBase.Awake)), HarmonyPostfix]
        static void Awake(wle.LevelEditorTriggerZoneActiveBase __instance)
        {
            __instance.StartActiveObject(true);
        }

        [HarmonyPatch(typeof(wle.LevelEditorTriggerZoneActiveBase), nameof(wle.LevelEditorTriggerZoneActiveBase.DisableTrigger)), HarmonyPostfix]
        static void DisableTrigger(wle.LevelEditorTriggerZoneActiveBase __instance)
        {
            __instance.StartActiveObject(false);
        }

        [HarmonyPatch(typeof(MPGNetObjectManager), nameof(MPGNetObjectManager.SpawnNetObject)), HarmonyPostfix]
        static void SpawnNetObject(MPGNetObjectManager __instance, GameMessageServerSpawnObject msg, ref GameObject __result)
        {
            msg.NetObjectSpawnData?.PostSpawnAction?.Invoke(msg.NetObjectSpawnData.NetID, __result);
        }

        [HarmonyPatch(typeof(COMMON_PlayerEliminationVolume), nameof(COMMON_PlayerEliminationVolume.CheckForVFX)), HarmonyPostfix]
        static void CheckForVFX(COMMON_PlayerEliminationVolume __instance, GameObject other)
        {
            if (other != null && other.TryGetComponent<FallGuysCharacterController>(out var fg))
                ServerGameStateActions.Instance.EliminateParticipant(fg.NetObject, false, LiveOps.Challenges.EliminationReason.Slime);
        }

        [HarmonyPatch(typeof(MPGNetObjectBootstrapper), nameof(MPGNetObjectBootstrapper.BootstrapObject)), HarmonyPostfix]
        static void BootstrapObject(MPGNetObjectBootstrapper __instance, Il2CppSystem.Action<MPGNetID, GameObject> postSpawnAction)
        {
            ServerGameStateActions.Instance.SetupNetworkObject(__instance, __instance.gameObject.transform.position, __instance.gameObject.transform.rotation, __instance.gameObject.transform.localScale, postSpawnAction);
        }

        [HarmonyPatch(typeof(RMIBehaviourManager), nameof(RMIBehaviourManager.SetActive)), HarmonyPostfix]
        static void SetActive()
        {
            RMIBehaviourManager.AllClientsConnected = true;
            RMIBehaviourManager.HandleRMIManagerActive?.Invoke();
        }

        [HarmonyPatch(typeof(MotorFunctionGrabStateGrabCrown), nameof(MotorFunctionGrabStateGrabCrown.OnGrab)), HarmonyPostfix]
        static void OnGrab(MotorFunctionGrabStateGrabCrown __instance, GrabTarget grabTarget)
        {
            var gtq = grabTarget.TargetGameObject.GetComponentInParent<COMMON_GrabToQualify>();
            gtq?.OnGrabbed(__instance.MotorAgent.Character.NetObject);
        }

        [HarmonyPatch(typeof(COMMON_TimeAttackTrigger), nameof(COMMON_TimeAttackTrigger.OnTriggerEnter)), HarmonyPrefix]
        static void OnTriggerEnter(COMMON_TimeAttackTrigger __instance, Collider other)
        {
            if (other.gameObject.TryGetComponent<FallGuysCharacterController>(out var controller))
            {
                if (__instance.IsEndZone)
                {
                    ServerManager.TimeAttackFinish?.Invoke(controller.NetObject);
                }
                else
                {
                    ServerManager.TimeAttackStart?.Invoke(controller.NetObject);
                }
            }
        }

        [HarmonyPatch(typeof(WallGuysSegmentGenerator), nameof(WallGuysSegmentGenerator.InstantiateRowGameObjects)), HarmonyPostfix]
        static void InstantiateRowGameObjects(WallGuysSegmentGenerator __instance, Il2CppSystem.Collections.Generic.List<GameObject> gameObjects, int rowIndex)
        {
            foreach (var spawned in __instance.GetComponentsInChildren<NetworkAwareGeneric>())
            {
                var targetObject = spawned.GetComponent<NetworkAwareGeneric>().SpawnObject;

                if (!targetObject.TryGetComponent<MPGNetObject>(out var netObj))
                    netObj = targetObject.AddComponent<MPGNetObject>();

                netObj.NetID = GlobalGameStateClient.Instance.NetObjectManager.GetNextNetID();
                netObj.GameObjectHash = netObj.GenerateGameObjectHash(NetObjectCreationMode.Spawn);

                netObj.SpawnPrefab(spawned.transform.position, spawned.transform.rotation, spawned.transform.localScale);

                if (!netObj.gameObject.TryGetComponent<OfflineGrabTargetID>(out var ogt))
                    ogt = netObj.gameObject.AddComponent<OfflineGrabTargetID>();

                ogt._hashID = (uint)UnityEngine.Random.Range(10000, 99999);
                ogt.Type = OfflineGrabTargetID.OfflineGrabTargetIDType.Grab | OfflineGrabTargetID.OfflineGrabTargetIDType.Mantle;

                spawned.gameObject.SetActive(false);
            }
        }

        [HarmonyPatch(typeof(COMMON_FakeDoorRandomiser), nameof(COMMON_FakeDoorRandomiser.Awake)), HarmonyPostfix]
        static void Awake(COMMON_FakeDoorRandomiser __instance)
        {
            if (__instance.TryGetComponent<MPGNetObjectPossessable>(out var poss))
                poss.PossessObject();
        }

        [HarmonyPatch(typeof(COMMON_GridPathRandomiser), nameof(COMMON_GridPathRandomiser.Awake)), HarmonyPostfix]
        static void Awake(COMMON_GridPathRandomiser __instance)
        {
            if (__instance.TryGetComponent<MPGNetObjectPossessable>(out var poss))
                poss.PossessObject();
        }

        [HarmonyPatch(typeof(BubbleZone), nameof(BubbleZone.ZoneBeginNetworkAction)), HarmonyPostfix]
        static void ZoneBeginNetworkAction(BubbleZone __instance)
        {
            __instance._numActiveBubbles = (int)Mathf.Max(__instance._playerCount * __instance._bubbleZoneConfig.BubblesPerPlayer, __instance._bubbleZoneConfig.MinActiveBubbles);
            __instance.RefreshAndReseedBubblePool();
            __instance.SpawnStartBubbles();
        }

        [HarmonyPatch(typeof(wle.LevelEditorSlimeVolume), nameof(wle.LevelEditorSlimeVolume.OnTriggerEnter)), HarmonyPostfix]
        static void OnTriggerEnter(COMMON_PlayerEliminationVolume __instance, Collider other)
        {
            if (other.TryGetComponent<MPGNetObject>(out var net) && net.IsFallGuy && CGM.GameRules.IsSurvivalRound)
                __instance.GameStateServerActioner.EliminateParticipant(net, false, LiveOps.Challenges.EliminationReason.Slime);
        }

        [HarmonyPatch(typeof(COMMON_PrefabSpawnerBase), nameof(COMMON_PrefabSpawnerBase.InstantiateObject)), HarmonyPostfix]
        static void InstantiateObject(COMMON_PrefabSpawnerBase __instance, COMMON_PrefabSpawnerBase.SpawnerEntry entry, Vector3 spawnPosition)
        {
            var control = __instance.GetComponent<PrefabSpawnerController>();
            //slop
            control.SpawnRequests.Add(new(GlobalGameStateClient.Instance.NetObjectManager._nextNetID - 1));
        }

        [HarmonyPatch(typeof(COMMON_PrefabSpawnerBase), nameof(COMMON_PrefabSpawnerBase.Awake)), HarmonyPostfix]
        static void Awake(COMMON_PrefabSpawnerBase __instance)
        {
            __instance.gameObject.AddComponent<PrefabSpawnerController>();
            foreach (var entry in __instance._spawnObjects)
            {
                entry.value.RemoveComponentIfExists<LodController>();

                if (!entry.value.TryGetComponent<ServerControlledObject>(out var serv))
                    serv = entry.value.AddComponent<ServerControlledObject>();
            }
        }

        [HarmonyPatch(typeof(wle.Levels.Obstacles.LevelEditorCommonPrefabSpawnerBase), nameof(wle.Levels.Obstacles.LevelEditorCommonPrefabSpawnerBase.Awake)), HarmonyPostfix]
        static void Awake(wle.Levels.Obstacles.LevelEditorCommonPrefabSpawnerBase __instance)
        {
            foreach (var entry in __instance._spawnObjects)
            {
                entry.value.RemoveComponentIfExists<LodController>();

                if (!entry.value.TryGetComponent<ServerControlledObject>(out var serv))
                    serv = entry.value.AddComponent<ServerControlledObject>();
            }
        }
    }
}
