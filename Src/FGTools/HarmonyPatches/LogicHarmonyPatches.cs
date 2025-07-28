extern alias wle;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using FG.Common;
using FG.Common.Character;
using FG.Common.LevelEvents;
using FG.Common.Rules;
using FGClient;
using FGTools.Services;
using FGTools.Services.Logic;
using FGTools.States;
using FGTools.States.Logic;
using HarmonyLib;
using Levels.Obstacles;
using Levels.WallGuys;
using MPG.Utility;
using System;
using UnityEngine;
using static FGTools.Services.LocalizationService;

namespace FGTools.HarmonyPatches
{
    public class LogicHarmony : FGTBase
    {
        //[HarmonyPatch(typeof(MotorFunctionSpeech), nameof(MotorFunctionSpeech.QueueItemsToConsiderSpamming), MethodType.Getter)]
        //[HarmonyPrefix]
        //static bool QueueItemsToConsiderSpamming(ClientGameStateView __instance, ref int __result)
        //{
        //    __result = int.MaxValue;
        //    return false;
        //}

        [HarmonyPatch(typeof(WallGuysSegmentGenerator), "Awake")]
        [HarmonyPrefix]
        static bool Awake(WallGuysSegmentGenerator __instance)
        {
            __instance._collider = __instance.gameObject.GetComponent<BoxCollider>();
            FGRandom.Create(__instance._objectId, __instance.GameState.RoundRandomSeed);
            __instance.CreateSegmentObstacles();
            __instance._collider.enabled = false;

            return false;
        }

        [HarmonyPatch(typeof(OnTriggerLevelEventEmitter), "OnEnable")]
        [HarmonyPrefix]
        static bool OnEnable(OnTriggerLevelEventEmitter __instance)
        {
            __instance._levelEventManager = LevelEventManager.Instance;
            __instance._definitionIndex = 0;
            __instance._initialised = true;
            return false;
        }

        //[HarmonyPatch(typeof(COMMON_SpawnBasket), "DoReturnCarryObjectToPool")]
        //[HarmonyPrefix]
        //static bool DoReturnCarryObjectToPool(COMMON_SpawnBasket __instance, GameObject carryObject, Il2CppSystem.Collections.Generic.Stack<GameObject> pool)
        //{
        //    carryObject.transform.parent = null;
        //    pool.Push(carryObject);
        //    __instance._spawnedItemGOs.Remove(carryObject);
        //    //var HideGUIHandler = carryObject.GetComponent<SpawnedObjectController>();
        //    //var pos = __instance.transform.position;
        //    //carryObject.gameObject.GetComponent<Rigidbody>().velocity = Vector3.zero;
        //    //carryObject.gameObject.transform.SetPositionAndRotation(new(pos.x, pos.y + 0.5f, pos.z), __instance.transform.rotation);
        //    return false;
        //}


        [HarmonyPatch(typeof(VictoryScreenViewModel), "Init")]
        [HarmonyPrefix]
        static bool Init(VictoryScreenViewModel __instance, uint winnerPlayerId, ClientPlayerManager clientPlayerManager, string winnerTimeAttackTime = null)
        {
            __instance._hasSkipped = false;
            __instance._victoryAnimFinishedTimestamp = float.MaxValue;
            __instance._animationFinishedEventSent = false;
            __instance._clientPlayerManager = clientPlayerManager;
            __instance._canSkip = false;
            __instance._timeAttackHolder.SetActive(winnerTimeAttackTime != null);
            __instance._timeAttackLapTimeText.SetText(winnerTimeAttackTime);
            __instance._winnerPlayerId = winnerPlayerId;
            if (__instance._createAnimationPropCoroutine != null)
            {
                __instance.StopCoroutine(__instance._createAnimationPropCoroutine);
                __instance._createAnimationPropCoroutine = null;
            }
            if (__instance._createAnimationPropCoroutine == null)
            {
                CoroutineRunner.Instance.StartCoroutine(StateManager.InternalState.PlayVictoryAnim(__instance).WrapToIl2Cpp());
            }
            return false;
        }

        [HarmonyPatch(typeof(VictoryScreenViewModel), "FinishInit")]
        [HarmonyPrefix]
        static bool FinishInit(VictoryScreenViewModel __instance, PlayerMetadata winnerPlayerMetadata, NetworkPlayerDataClient winnerNetworkPlayerDataClient, GameObject animProp)
        {
            __instance.ConfigureWinnersText(__instance._localisedStrings.GetString("winner"), Color.white, __instance._winnerTextOutlineColor);
            __instance.ConfigureWinnerPlayer(winnerPlayerMetadata, winnerNetworkPlayerDataClient, true, __instance._fallguySpawnPosition, animProp, 0);
            __instance.SetupSkipPromptRoutine();
            return false;
        }


        [HarmonyPatch(typeof(VictoryScreenViewModel), "Update")]
        [HarmonyPrefix]
        static bool Update(VictoryScreenViewModel __instance)
        {
            foreach (NameTagViewModel tag in Resources.FindObjectsOfTypeAll<NameTagViewModel>())
                try { tag.UpdateDisplayWithLocalPlayer(); } catch { }
            if (__instance.HasVictoryAnimFinish && !__instance._animationFinishedEventSent && !ReportManager.Instance.IsPlayerReportWindowOpen && !ReportManager.Instance.IsPlayerReportListWindowOpen && (PartyStateManager.Instance.PartyMenu == null || !PartyStateManager.Instance.PartyMenu.IsExpanded))
            {
                var state = StateManager.GetState<GameplayState>();
                if (state.winResultsPending || state.timeAttackWinResultsPending)
                {
                    state.winResultsPending = false;
                    state.AfterWinPopups(state.timeAttackWinResultsPending, __instance);
                    __instance.StopMusicImmediately();
                    __instance.Skip();
                    state.timeAttackWinResultsPending = false;

                }
                __instance._animationFinishedEventSent = true;
            }

            return false;
        }

        [HarmonyPatch(typeof(GameRules), nameof(GameRules.PickStartingPosition)), HarmonyPrefix]
        static bool PickStartingPosition(GameRules __instance, int entityId, uint squadId, int teamId, int vsGroupId, bool isSquadShow)
        {
            int groupingId = __instance.GetGroupingId(entityId, squadId, teamId, vsGroupId, isSquadShow);
            StartingPositionAllocator groupPositions = __instance.GetStartingPositionsCandidates(groupingId);
            return groupPositions.GetRandomPosition();
        }

     
        [HarmonyPatch(typeof(TimeAttackLeaderboardViewModel), nameof(TimeAttackLeaderboardViewModel.UpdateScoreUI))]
        static bool NullPatch()
        {
            return false;
        }
    }

}
