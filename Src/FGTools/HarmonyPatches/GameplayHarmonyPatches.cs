using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using DG.Tweening;
using Events;
using FG.Common;
using FG.Common.Audio;
using FG.Common.Character;
using FG.Common.CMS;
using FG.Common.LevelEvents;
using FG.Common.LevelEvents.Handlers;
using FG.Common.LODs;
using FG.Common.Network;
using FGClient;
using FGTools.Internal.Behaviours;
using FGTools.LocalServer;
using FGTools.Services;
using FGTools.States;
using FGTools.States.Logic;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Levels;
using Levels.ChickenChase;
using Levels.JumpShowdown;
using Levels.KrakenAttack;
using Levels.Obstacles;
using Levels.PixelPerfect;
using Levels.Progression;
using Levels.ScoreZone;
using Levels.SeeSaw;
using Levels.SnowballSurvival;
using Levels.SnowyScrap;
using Levels.TimeAttack;
using Levels.TipToe;
using LiveOps.Collections;
using LiveOps.TimeAttack;
using Mediatonic.Tools.Utils;
using SRF;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using static FGTools.Config.ConfigManager;
using static FGTools.Internal.Extensions.FLZ_Extensions;
using static Levels.Obstacles.COMMON_PrefabSpawnerBase;
using static UnityEngine.Object;
using Collision = UnityEngine.Collision;

namespace FGTools.HarmonyPatches
{
    public class FranticExplorer : FGTBase
    {
        [HarmonyPatch(typeof(ScoredButton), "SetAsActiveTarget")]
        [HarmonyPrefix]
        static bool SetAsActiveTarget(ScoredButton __instance, uint managerId)
        {
            __instance._isAnActiveTarget = true;
            __instance._managerInstanceId = managerId;
            bool isGamePlaying = __instance.GameState.IsGamePlaying;
            __instance.AllowButtonToReset(true);
            __instance.ImmediatelySetButtonAsPrimed();
            __instance.UpdateVisuals(__instance._isAnActiveTarget);
            return false;
        }
    }

    public class SelfRespawerFix : FGTBase
    {
        [HarmonyPatch(typeof(COMMON_SelfRespawner), nameof(COMMON_SelfRespawner.Awake)), HarmonyPrefix]
        static bool Awake(COMMON_SelfRespawner __instance)
        {
            if (__instance._rigidbody == null)
                __instance._rigidbody = __instance.GetComponent<Rigidbody>();

            if (__instance._netObject == null)
                __instance._netObject = __instance.GetComponent<MPGNetObject>();

            if (__instance._respawnFX == null)
                __instance._respawnFX = __instance.GetComponent<SelfRespawnerFX>();

            PhysicsManager.Instance.RegisterCommonSelfRespawnerRigidbody(__instance.gameObject, __instance);
            return false;
        }

        [HarmonyPatch(typeof(COMMON_SelfRespawner), nameof(COMMON_SelfRespawner.TryToRespawn)), HarmonyPrefix]
        static bool TryToRespawn(COMMON_SelfRespawner __instance)
        {
            if (__instance.CanRespawn)
            {
                if (__instance._netObject == null)
                    __instance._netObject = __instance.GetComponent<MPGNetObject>();

                Vector3 respawnPosition = __instance.RespawnPosition;
                Quaternion respawnRotation = __instance.RespawnRotation;

                if (__instance._respawnCheckBoundsCollider && !__instance.CanRespawnAtPosition(__instance.RespawnPosition, __instance.RespawnRotation))
                    __instance.TryGetAvailableFallbackPosition(out respawnPosition, out respawnRotation);

                __instance._respawnLookingForAvailableSpot = false;

                if (__instance._respawnFX)
                {
                    if (__instance.OnRespawn != null)
                        __instance.OnRespawn.Invoke();

                    __instance._respawnFX.RequestPlayRespawnVFX(respawnPosition);
                    AudioManager.PlayOneShot("SFX_OBJ_ItemRespawnable_DropItem");
                    __instance._respawnCoroutine = __instance.StartCoroutine(__instance.PlayVFXAndRespawn(respawnPosition, respawnRotation));
                }
                else
                    __instance.DoRespawn(respawnPosition, respawnRotation);
            }
            return false;
        }

        //[HarmonyPostfix]
        //[HarmonyPatch(typeof(COMMON_SelfRespawner), nameof(COMMON_SelfRespawner.KillPlaneYThreshold), MethodType.Getter)]
        //static void KillPlaneYThreshold(ref float __result)
        //{
        //    __result = FGBehaviour.respawnPos;
        //}

        [HarmonyPatch(typeof(SelfRespawnerFX), nameof(SelfRespawnerFX.RequestPlayRespawnVFX)), HarmonyPrefix]
        static bool RequestPlayRespawnVFX(SelfRespawnerFX __instance, Vector3 pos)
        {
            __instance.PlayRespawnVFX(pos);
            return false;
        }

        [HarmonyPatch(typeof(SelfRespawnerFX), nameof(SelfRespawnerFX.RequestToggleRenderersAndColliders)), HarmonyPrefix]
        static bool RequestToggleRenderersAndColliders(SelfRespawnerFX __instance, bool state)
        {
            __instance.DoToggleRenderersAndColliders(state);
            return false;
        }
    }

    public class AttackOfTheTime : FGTBase
    {
        public static TimeAttackPlayerStats PlayerStats
        {
            get { return FGTController.TimeAttackManager.GetPlayerStats(102); }
        }

        public static TimeAttackLapDisplay Display
        {
            get { return Resources.FindObjectsOfTypeAll<TimeAttackLapDisplay>().FirstOrDefault(); }
        }

        static List<TimeAttackItem> Bubbles = new();

        static bool BubblesReady = false;

        public static void Reset()
        {
            BubblesReady = false;
            Bubbles.Clear();
        }

        public static void SetupTimeBubbles()
        {
            if (BubblesReady)
                return;

            List<Transform> zone = [];
            List<string> valid = [];
            var itemManager = Resources.FindObjectsOfTypeAll<TimeAttackItemManager>().FirstOrDefault();
            var knownSpawns = CMSLoader.Instance.CMSData.TimeAttackSpawns;
            TimeAttackSpawnSchema spawnData = null;

            foreach (var a in knownSpawns)
            {
                if (a.key.Split('_')[1].Contains(StateManager.CurrentRound.Id.Split('_')[1]))
                {
                    spawnData = a.Value;
                    break;
                }
            }

            if (spawnData == null)
            {
                FGTLog(LogLevel.Error, "", "Level doesn't have info for time attack in the CMS");
                return;
            }

            itemManager.TimeAttackSpawnSchema = spawnData;

            foreach (var shit in itemManager.TimeAttackItemZones)
            {
                TimeAttackZone zone2 = spawnData.Zones.ToList().Find(x => x.ZoneId.Equals(shit.ZoneId));
                shit.Zone.gameObject.SetActive(true);
                shit.Zone.Init(itemManager, zone2, shit);
            }

            foreach (TimeAttackItemZone shit in Resources.FindObjectsOfTypeAll<TimeAttackItemZone>())
            {
                if (shit.TimeAttackItemZoneMapping.Groups.Count > 0)
                {
                    for (int i = 0; i < shit.TimeAttackItemZoneMapping.Groups.Count; i++)
                    {
                        if (shit.TimeAttackItemZoneMapping.Groups[i].GroupId != "Off" && shit.TimeAttackItemZoneMapping.Groups[i].Group.gameObject.transform.childCount > 0)
                        {
                            valid.Add(shit.TimeAttackItemZoneMapping.Groups[i].GroupId);
                        }
                    }
                }

                if (valid.Count > 0)
                {
                    int lastRandId = UnityEngine.Random.RandomRange(0, valid.Count);
                    shit.InvokeToggleGroup(valid[lastRandId]);
                }
            }

            foreach (var a in Resources.FindObjectsOfTypeAll<TimeAttackItem>().ToList().FindAll(x => x.gameObject.activeSelf))
                Bubbles.Add(a);

            BubblesReady = true;
        }


        [HarmonyPatch(typeof(TimeAttackItemTrigger), "OnEnterSensor")]
        [HarmonyPrefix]
        static bool OnEnterSensor(TimeAttackItemTrigger __instance, COMMON_TriggerVolume.SensorData sensorData)
        {
            if (__instance.TimeAttackItemZone != null && sensorData.netObject != null && !__instance._triggeredPlayers.Contains(sensorData.netObject.NetID) && __instance.TimeAttackItem != null)
            {
                int time = __instance.TimeAttackItem.Value;
             
                __instance._triggeredPlayers.Add(FallGuyBehaviour._instance.FGMPG.NetID);
                __instance.TimeAttackItemZone.DoTrigger(__instance.Idx, FallGuyBehaviour._instance.FGMPG);
                Broadcaster.Instance.Broadcast(new TimeAttackItemTriggeredEvent(FallGuyBehaviour._instance.FGMPG.NetID, time));
                PlayerStats.HandleBubbleTime(time);
                StateManager.UIM.GetComponentInChildren<TimeAttackItemPopupManagerViewModel>().HandleTimeAttackItemCollected(new() { pauseTime = time });
                __instance.TimeAttackItem.DoTrigger();
            }

            return false;
        }

        [HarmonyPatch(typeof(COMMON_TimeAttackTrigger), "OnTriggerEnter")]
        [HarmonyPrefix]
        static bool COMMON_TimeAttackTrigger(COMMON_TimeAttackTrigger __instance, Collider other)
        {

            if (__instance.GameState.IsPlayerCollider(other, out FallGuysCharacterController controller))
            {
                var state = PlayerStats.GetCurrentLap.LapState;
                FGTController.TimeAttackManager.UpdateRankings();

                if (!__instance._isEndZone && state == TimeAttackLapState.NotStarted)
                {
                    FGTLog(LogLevel.Info, null, "Time Attack Start");
                    Broadcaster.Instance.Broadcast(new TimeAttackLapStart { NetObject = controller.NetObject.NetID });
                    PlayerStats.StartTrackingLap();
                    AudioMixing.Instance.ResetTimeAttackParams();
                    AudioManager.Instance.PlayOneShot(AudioManager.Instance._eventMasterData.TimeAttackTimeStart, default);
                    Display._currentLocalTimeAttackLapState = TimeAttackLapState.InProgress;
                    FGBehaviour.ReDisplaySkipBtns(true);
                }
                else if (state == TimeAttackLapState.InProgress && __instance._isEndZone)
                {
                    FGTLog(LogLevel.Info, null, "Time Attack Finish");
                    Display._currentLocalTimeAttackLapState = TimeAttackLapState.Finished;
                    //Display.ShouldShowTimeAttackResetInput = false;
                    Broadcaster.Instance.Broadcast(new TimeAttackLapComplete { NetObject = controller.NetObject.NetID });
                    PlayerStats.CompleteLap();
                    AudioMixing.Instance.StartTimeAttackSnapshot();
                    AudioManager.Instance.PlayOneShot(AudioManager.Instance._eventMasterData.TimeAttackBestTime, default);
                    Transform trans = CGM.GameRules.PickRespawnPosition(102, 0, FGBehaviour.PlayerTeamId, 0, false).gameObject.transform;
                    FallGuyBehaviour._instance.FGCC.TeleportMotorFunction.RequestTeleport(trans.position, trans.rotation);
                    FallGuyBehaviour._instance.FGCC.ResetToDefaultState();
                    StateManager.UIM.HandleTimeAttackShowEndOfRunEvent(new());
                    FGTServiceManager.GetService<RoundLoaderService>().RoundCamera.OnRecenterAndSnapCameraNextFrameRequested();
                    PlayerStats.ResetPlayer();
                    Resources.FindObjectsOfTypeAll<TimeAttackItemManager>().FirstOrDefault().TryResetPlayerItems(FallGuyBehaviour._instance.FGMPG);
                    foreach (var bubble in Bubbles)
                        bubble.ResetItem();
                    //FGBehaviour._gpModel.ShowRestartButton = false;
                }
            }
            return false;
        }

        [HarmonyPatch(typeof(GameplayTimeAttackEndOfRunViewModel), "UpdateScreen")]
        [HarmonyPrefix]
        static bool UpdateScreen(GameplayTimeAttackEndOfRunViewModel __instance)
        {
            __instance.UpdateTitleText();
            float[] sortedLapTimes = PlayerStats.GetSortedLapTimes;
            float currentLapTime = PlayerStats.GetLapTimes.Last();
            float bestTime = sortedLapTimes[0];
            bool isNewBest = TimeAttackManager.IsTimeZero(bestTime - currentLapTime);
            Il2CppSystem.Nullable<float> splitTime = null;
            string largeTitle = __instance._localisedStrings.GetString(isNewBest ? "timeattack_new_best" : "timeattack_this_run");
            __instance._bestScoreDisplay._dangerIcon.SetActive(false);
            if (sortedLapTimes.Length > 1)
            {
                splitTime = new Il2CppSystem.Nullable<float>(currentLapTime - (isNewBest ? sortedLapTimes[1] : bestTime));
                __instance._bestScoreDisplay.Setup(currentLapTime, splitTime, largeTitle, false);
            }
            else
            {
                __instance._bestScoreDisplay._lapTimeText.SetText(UIUtils.CreateTimeText(currentLapTime, true, true, 80f));
                __instance._bestScoreDisplay._splitText.gameObject.SetActive(false);
                __instance._bestScoreDisplay._largeTitleText.SetText(largeTitle);
            }
            return false;
        }

        [HarmonyPatch(typeof(GameplayTimeAttackEndOfRunViewModel), "OnResetButtonPressed")]
        [HarmonyPrefix]
        static bool OnResetButtonPressed(GameplayTimeAttackEndOfRunViewModel __instance)
        {
            //Broadcaster.Instance.RaiseEvent(default(OnTimeAttackResetInputPressed));
            Broadcaster.Instance.RaiseEvent(default(TimeAttackHideEndOfRunEvent));
            FGTServiceManager.GetService<RoundLoaderService>().RoundCamera.OnRecenterAndSnapCameraNextFrameRequested();
            PlayerStats.AddNewLap(TimeAttackUpdateType.Restart);
            return false;
        }

        //[HarmonyPatch(typeof(ClientGameManager), "OnLocalPlayerTimeAttackResetRequest")]
        //[HarmonyPrefix]
        //static bool OnLocalPlayerTimeAttackResetRequest(ClientGameManager __instance, OnLocalPlayerTimeAttackResetRequest evt)
        //{
        //    if (StateManager.CGM.GameRules.IsTimeAttackGameMode)
        //    {
        //        FallGuyBehaviour._instance.RespawnPlayer(true);
        //        PlayerStats.RestartCurrentLap();
        //        Display._currentLocalTimeAttackLapState = TimeAttackLapState.NotStarted;
        //        Display.ShouldShowTimeAttackResetInput = false;
        //        Resources.FindObjectsOfTypeAll<TimeAttackItemManager>().FirstOrDefault().TryResetPlayerItems(FallGuyBehaviour._instance.FGMPG);
        //    }
        //    else if (enableSpeedrunMode.Value)
        //    {
        //        FGTBehaviour.StartCoroutine(ServiceManagerFGT.Instance.GetService<SpeedrunService>().NewRun().WrapToIl2Cpp());
        //        Display._currentLocalTimeAttackLapState = TimeAttackLapState.NotStarted;
        //        Display.ShouldShowTimeAttackResetInput = false;
        //    }
        //    return false;
        //}
    }

    public class GlobalGameplayPatch : FGTBase
    {
        [HarmonyPatch(typeof(MotorFunctionPortalStateActive), "End")]
        [HarmonyPrefix]
        public static bool MF_PSA_End(ref MotorFunctionPortalStateActive __instance, int nextState)
        {
            __instance._motorFunctionPortal.PortalData.Clear();
            return false;
        }

        [HarmonyPatch(typeof(COMMON_GridPathRandomiser), "Awake")]
        [HarmonyPrefix]
        static bool GPR_Awake(COMMON_GridPathRandomiser __instance)
        {
            __instance.GeneratePath(FallGuyBehaviour.ThisRoundRandom());
            __instance.CreatePathFromData();
            return false;
        }

        [HarmonyPatch(typeof(ChickenChaseController), nameof(ChickenChaseController.Start))]
        [HarmonyPrefix]
        static bool Start(ChickenChaseController __instance)
        {
            return false;
        }

        [HarmonyPatch(typeof(ChickenChaseController), "PerformTickLogic")]
        [HarmonyPrefix]
        static bool PerformTickLogic()
        {
            foreach (CarryObject obj in Resources.FindObjectsOfTypeAll<CarryObject>())
            {
                if (obj.CarriedByCharacter != null)
                {
                    int points = obj.gameObject.GetComponent<ChickenController>().scoreValue.PointsAmount;
                    if (!CGM.GameRules.IsTeamGameMode)
                        CGM._soloScoreManager.AwardSoloPoints(obj.CarriedByCharacter.NetObject.NetID, points);
                    else
                    {
                        int team = obj.CarriedByCharacter.TeamID;
                        StateManager.PTM.AwardTeamPoints(team, points);
                        StateManager.GetState<GameplayState>().UpdateTeamsUI(team, StateManager.PTM.GetTeamScore(team));
                    }
                }
            }
            return false;
        }

        [HarmonyPatch(typeof(COMMON_SeeSaw360), "Awake")]
        [HarmonyPrefix]
        private static bool Awake(COMMON_SeeSaw360 __instance)
        {
            __instance._rb = __instance.gameObject.GetComponent<Rigidbody>();
            __instance.LimitAngularVelocity();
            return false;
        }

        [HarmonyPatch(typeof(COMMON_SnowballSurvivalBall), "Awake")]
        [HarmonyPrefix]
        static bool SSB_Awake()
        {
            return false;
        }

        [HarmonyPatch(typeof(KrakenAttackRetractableTile), "OnTouchedByPlayer")]
        [HarmonyPrefix]
        static bool KART_OnTouchedByPlayer(KrakenAttackRetractableTile __instance, FallGuysCharacterController fgcc, Collision collision)
        {
            if (__instance.IsAlive && __instance.IsCollisionWithTopFace(collision) && __instance.GameState.SimulationTime - __instance._lastDamageTime >= __instance._damageCooldown)
            {
                __instance.TakeDamageRMI();
                __instance._lastDamageTime = __instance.GameState.SimulationTime;
                if (__instance.IsAlive == false)
                {
                    KrakenAttackManager m = Resources.FindObjectsOfTypeAll<KrakenAttackManager>().FirstOrDefault();
                    m._tentaclesPendingToShowCount++;
                    m.ShowPendingTentacles();
                }
            }
            return false;
        }


        [HarmonyPatch(typeof(COMMON_BreakableIceTile), "FixedUpdate")]
        [HarmonyPrefix]
        static bool BIT_FixedUpdate(COMMON_BreakableIceTile __instance)
        {
            __instance._crackCooldown -= __instance.GameState.SimulationFixedDeltaTime;
            return false;
        }

        [HarmonyPatch(typeof(COMMON_BreakableIceTile), "OnTriggerLogic")]
        [HarmonyPrefix]
        static bool BIT_OnTriggerLogic(COMMON_BreakableIceTile __instance, Collider col)
        {
            if (StateManager.IsInGameplay)
            {
                __instance.HandleStoodOnPlatformServer(col);
            }
            return false;
        }

        [HarmonyPatch(typeof(TipToe_Platform), "HandleRealPlatformTriggerEntered")]
        [HarmonyPrefix]
        static bool TTP_HandleRealPlatformTriggerEntered(TipToe_Platform __instance, bool fromExplosionDetection)
        {
            __instance.OnRealPlatformLightOn();
            if (fromExplosionDetection)
                __instance.StartCoroutine(__instance.DelayedRealPlatformTriggerExit());
            return false;
        }

        [HarmonyPatch(typeof(TipToe_Platform), "HandleFakePlatformTriggerEntered")]
        [HarmonyPrefix]
        static bool TTF_HandleFakePlatformTriggerEntered(TipToe_Platform __instance, Collider triggeringCollider)
        {
            __instance.HandleFakePlatformTriggeredOnClient(triggeringCollider);
            return false;
        }

        [HarmonyPatch(typeof(TipToe_Platform), "HandleRealPlatformTriggerExited")]
        [HarmonyPrefix]
        static bool TTP_HandleRealPlatformTriggerExited(TipToe_Platform __instance)
        {
            __instance.OnRealPlatformLightOff();
            return false;
        }

        [HarmonyPatch(typeof(TipToe_Platform), "HandleFakePlatformTriggeredOnClient")]
        [HarmonyPrefix]
        static bool TTF_HandleFakePlatformTriggeredOnClient(TipToe_Platform __instance, Collider triggeringCollider)
        {
            triggeringCollider.gameObject.TryGetComponent(out FallGuysCharacterController fgcc);
            __instance.OnFakePlatformSteppedOn();
            if (fgcc != null)
                __instance.NudgeCharacterIntoVoidAndDisableJumpBuffer(triggeringCollider, fgcc);
            return false;
        }

        [HarmonyPatch(typeof(COMMON_Button), "OnCollisionStay")]
        [HarmonyPrefix]
        static bool B_OnCollisionStay(COMMON_Button __instance, Collision col)
        {
            if (__instance._currentButtonState == COMMON_Button.ButtonState.Primed)
            {
                __instance.PressButton(GlobalGameStateClient.Instance.GameStateView.SimulationFixedTime);
                if (__instance.TryCast<ScoredButton>() != null && __instance.TryCast<ScoredButton>()._isAnActiveTarget && FallGuyBehaviour._instance.controller.GetComponent<FFAButtonManager>() != null)
                {
                    FallGuyBehaviour._instance.controller.GetComponent<FFAButtonManager>().PushButton(__instance.Cast<ScoredButton>());
                }
            }
            else if (__instance._currentButtonState == COMMON_Button.ButtonState.ReturningToPrimed)
                __instance.TryApplyResetLaunchForce(col);
            return false;
        }

        [HarmonyPatch(typeof(OnTriggerLevelEventEmitter), "OnEnable")]
        [HarmonyPrefix]
        static bool OnTriggerLevelEventEmitterStart(OnTriggerLevelEventEmitter __instance)
        {
            __instance._levelEventManager = LevelEventManager.Instance;
            __instance._definitionIndex = 0;
            __instance._initialised = true;

            return false;
        }

    }

    public class MotorPatches : FGTBase
    {
        [HarmonyPatch(typeof(MotorFunctionGrabStateGrabCrown), "OnGrab")]
        [HarmonyPrefix]
        static bool MF_GSGC_OnGrab(MotorFunctionGrabStateGrabCrown __instance, GrabTarget grabTarget)
        {
            if (WinLevel.Value != WinType.None)
            {
                //if (WinLevel.Value != WinType.JustCrownGrab)
                //{
                //    StateManager.GetState<GameplayState>().DoWin();
                //    grabTarget.TargetGameObject.gameObject.GetComponent<Animation>().enabled = false;
                //}
                return true;
            }
            else
            {
                __instance.End(0);
                FallGuyBehaviour._instance.FGCC.ResetToDefaultState();
            }
            return false;
        }

        [HarmonyPatch(typeof(MotorFunctionSwingStateGrab), nameof(MotorFunctionSwingStateGrab.Begin), MethodType.Normal), HarmonyPostfix]
        static void ServerConfirmedGrabWorkaround(ref MotorFunctionSwingStateGrab __instance, int prevState)
        => __instance.HasServerConfirmedGrab = true;
    }


    public class BlastBallFix : FGTBase
    {
        [HarmonyPatch(typeof(COMMON_BlastBall), nameof(COMMON_BlastBall.StartBlastSequence))]
        [HarmonyPrefix]
        static bool StartBlastSequence(COMMON_BlastBall __instance)
        {
            if (__instance._state == COMMON_BlastBall.BlastBallState.Primed)
            {
                __instance._explosionEventDefinitionIndex = 1;
                __instance.StartSequenceEvent(__instance.GameState.GameplayTimeElapsed, false);
            }
            return false;
        }

        [HarmonyPatch(typeof(COMMON_BlastBall), nameof(COMMON_BlastBall.SendExplosionLevelEvent))]
        [HarmonyPrefix]
        static bool SendExplosionLevelEvent(COMMON_BlastBall __instance)
        {
            var pos = new Vector3(__instance.transform.position.x, __instance.transform.position.y, __instance.transform.position.z);
            var handler = __instance._levelEventManager.GetHandlerAt(__instance._explosionEventDefinitionIndex).Cast<BlastBallExplosionEventHandler>();
            handler.HandleLevelEvent(pos, __instance.ObjectIDString, (uint)FallGuyBehaviour._instance.FGMPG.NetID);
            return false;
        }

        [HarmonyPatch(typeof(COMMON_BlastBall), nameof(COMMON_BlastBall.SendOnRespawnEvent))]
        [HarmonyPrefix]
        static bool SendOnRespawnEvent(COMMON_BlastBall __instance)
        {
            __instance.OnRespawn();
            return false;
        }

        [HarmonyPatch(typeof(COMMON_BlastBall), nameof(COMMON_BlastBall.IsLevelEditor), MethodType.Getter)]
        [HarmonyPrefix]
        static bool IsLevelEditor(COMMON_BlastBall __instance, ref bool __result)
        {
            __result = true;
            return false;
        }
    }

    public class GlobalScoreGamesPatch : FGTBase
    {

        [HarmonyPatch(typeof(ScoreAwarder), "AwardPoints")]
        [HarmonyPrefix]
        static bool AwardPoints(ScoreAwarder __instance, GameObject go)
        {
            int pointsAmount = __instance.CalculatePoints(go);
            //pointsCounter += pointsAmount;
            __instance.ApplyPointsUpdate(pointsAmount, go);
            return false;
        }

        [HarmonyPatch(typeof(ScoreAwarder), "ApplyPointsUpdate")]
        [HarmonyPrefix]
        static bool ApplyPointsUpdate(ScoreAwarder __instance, int pointsAmount, GameObject go)
        {
            switch (__instance._scoringType)
            {
                case ScoreAwarder.ScoringTypes.DefinedTeamScores:

                    StateManager.PTM.AwardTeamPoints(__instance.TeamID, pointsAmount);
                    int totalScore = StateManager.PTM.GetTeamScore(__instance.TeamID);
                    StateManager.GetState<GameplayState>().UpdateTeamsUI(__instance.TeamID, totalScore);
                    break;
                case ScoreAwarder.ScoringTypes.PlayerScores:
                    {
                        MPGNetObject mpg = go.GetComponent<MPGNetObject>();
                        if (mpg != null)
                            CGM._soloScoreManager.AwardSoloPoints(mpg.NetID, pointsAmount);
                        break;
                    }
                case ScoreAwarder.ScoringTypes.TeamOwnedScores:
                    {
                        TeamOwned teamOwned = go.GetComponent<TeamOwned>();
                        if (teamOwned != null)
                            StateManager.PTM.AwardTeamPoints(teamOwned.TeamID, pointsAmount);
                        break;
                    }
            }
            return false;
        }
    }

    public class TQOSTPatch : FGTBase
    {
        [HarmonyPatch(typeof(TeamQualificationObjectsScoreTracker), "Start")]
        [HarmonyPrefix]
        static bool Start(TeamQualificationObjectsScoreTracker __instance)
        {
            __instance.Initialise();
            return false;
        }

        [HarmonyPatch(typeof(TeamQualificationObjectsScoreTracker), "FixedUpdate")]
        [HarmonyPrefix]
        static bool FixedUpdate(TeamQualificationObjectsScoreTracker __instance)
        {
            if (StateManager.IsInGameplay)
            {
                __instance._currentPollingTimer += Time.deltaTime;
                bool flag2 = __instance._currentPollingTimer >= 0.25f;
                if (flag2)
                {
                    foreach (COMMON_TeamQualificationObject ball in __instance._teamQualificationObjects)
                    {
                        float score = 100f * ball.GetProgressPercentage();
                        StateManager.PTM.SetTeamScore(ball.TeamId, (int)score);
                        StateManager.GetState<GameplayState>().UpdateTeamsUI(ball.TeamId, (int)score);
                        if (StateManager.PTM.GetTeamScore(ball.TeamId) == 100)
                        {
                            if (ball.TeamId != FGBehaviour.PlayerTeamId)
                                StateManager.GetState<GameplayState>().DoElim();
                            else
                                StateManager.GetState<GameplayState>().DoQual();
                        }
                    }
                    __instance._currentPollingTimer -= 0.25f;
                }
            }
            return false;
        }
    }

    public class TCTPatch : FGTBase
    {
        [HarmonyPatch(typeof(TerritoryControl_Tile), "OnCollisionEnter")]
        [HarmonyPrefix]
        static bool OnCollisionEnter(TerritoryControl_Tile __instance, Collision collision)
        {
            if (collision.gameObject.TryGetComponent<FallGuysCharacterController>(out var fgcc) && fgcc.IsCarrying)
            {
                fgcc.OnStartCarrying += new Action<CarryObject>(__instance.OnFallGuyStartCarrying);
                fgcc.OnStopCarrying += new Action<CarryObject, FallGuysCharacterController.StopCarryingReason>(__instance.OnFallGuyStopCarrying);
                __instance.RecalculateTileOwnership(fgcc, true);
            }
            return true;
        }

        [HarmonyPatch(typeof(TerritoryControl_Tile), "OnCollisionExit")]
        [HarmonyPrefix]
        static bool OnCollisionExit(TerritoryControl_Tile __instance, Collision collision)
        {
            if (collision.gameObject.TryGetComponent<FallGuysCharacterController>(out var fgcc) && fgcc.IsCarrying)
            {
                fgcc.OnStartCarrying -= new Action<CarryObject>(__instance.OnFallGuyStartCarrying);
                fgcc.OnStopCarrying -= new Action<CarryObject, FallGuysCharacterController.StopCarryingReason>(__instance.OnFallGuyStopCarrying);
                __instance.RecalculateTileOwnership(fgcc, false);
            }
            return false;
        }

        [HarmonyPatch(typeof(TerritoryControl_Tile), "AwardTileToTeam")]
        [HarmonyPrefix]
        static bool AwardTileToTeam(TerritoryControl_Tile __instance, int teamIndex, bool contested = false)
        {
            if (__instance._tileOwnerTeamID >= 0)
            {
                StateManager.PTM.AwardTeamPoints(__instance._tileOwnerTeamID, -1);
                StateManager.GetState<GameplayState>().UpdateTeamsUI(__instance._tileOwnerTeamID, StateManager.PTM.GetTeamScore(teamIndex));
            }
            __instance._tileOwnerTeamID = teamIndex;
            if (__instance._tileOwnerTeamID >= 0)
            {
                StateManager.PTM.AwardTeamPoints(__instance._tileOwnerTeamID, 1);
                StateManager.GetState<GameplayState>().UpdateTeamsUI(__instance._tileOwnerTeamID, StateManager.PTM.GetTeamScore(teamIndex));
            }
            bool isActive = __instance._tileOwnerTeamID != -1 || contested;
            float teamControl;
            if (contested)
                teamControl = 0f;
            else
            {
                switch (teamIndex)
                {
                    case 0:
                        teamControl = 0.735f;
                        break;
                    case 1:
                        teamControl = -0.735f;
                        break;
                    default:
                        return false;
                }
            }
            __instance.UpdateTileVisuals(isActive, teamControl);
            return false;
        }
    }

    public class ScoringPatch : FGTBase
    {
        [HarmonyPatch(typeof(BubbleZone), "OnBubbleBurst")]
        [HarmonyPrefix]
        static bool OnBubbleBurst(BubbleZone __instance, SpawnableCollectable bubble, MPGNetObject playerNetObj)
        {
            __instance.LogBubbleStats("bursting");
            BubblePool burstBubblePool = __instance._weightedBubblePools.PeekItemByIndex(__instance._bubblePoolIndices[bubble]).Entry;
            CGM._soloScoreManager.AwardSoloPoints(playerNetObj.NetID, burstBubblePool.Value);
            int oldBubbleIdx = burstBubblePool.GetItemIndex(bubble);
            __instance._remainingScore -= burstBubblePool.Value;
            Il2CppReferenceArray<Il2CppSystem.Object> args = new Il2CppSystem.Object[2];
            args[0] = __instance._bubblePoolIndices[bubble];
            args[1] = oldBubbleIdx;
            __instance._totalSpawnedScore -= burstBubblePool.Value;
            __instance.InvokeMethodEverywhere(__instance._burstBubbleNetworkActionIdx, args);
            if (__instance._totalSpawnedScore == 0)
            {
                Il2CppSystem.Action<ScoreZone> onZoneEnd = __instance._onZoneEnd;
                if (onZoneEnd != null)
                    onZoneEnd.Invoke(__instance);
            }

            return false;
        }

        [HarmonyPatch(typeof(CollectionZone), "RegisterCollection")]
        [HarmonyPrefix]
        static bool RegisterCollection(CollectionZone __instance, SpawnableCollectable collectable, MPGNetObject playerNetObj)
        {
            CGM._soloScoreManager.AwardSoloPoints(playerNetObj.NetID, collectable.Value);
            Il2CppReferenceArray<Il2CppSystem.Object> args = new Il2CppSystem.Object[1];
            args[0] = __instance._collectableIndices[collectable];
            __instance.InvokeMethodEverywhere(__instance._registerCollectionNetworkActionIdx, args);
            __instance._remainingCollectablesCount -= 1;
            if (__instance._remainingCollectablesCount == 0)
            {
                Il2CppSystem.Action<ScoreZone> onZoneEnd = __instance._onZoneEnd;
                if (onZoneEnd != null)
                    onZoneEnd.Invoke(__instance);
            }
            return false;
        }

        [HarmonyPatch(typeof(VolumeZone), "AwardPointsAndGetTotal")]
        [HarmonyPrefix]
        static bool AwardPointsAndGetTotal(VolumeZone __instance)
        {
            __instance._scorePerPlayerThisFrame = Time.deltaTime * __instance._volZoneConfig.ScorePerSecond;
            foreach (FallGuysCharacterController fgcc in __instance.ZoneOccupants)
            {
                if (!__instance._accumulatedScoresByPlayer.ContainsKey(fgcc))
                    __instance._accumulatedScoresByPlayer.Add(fgcc, 0f);
                Il2CppSystem.Collections.Generic.Dictionary<FallGuysCharacterController, float> accumulatedScoresByPlayer = __instance._accumulatedScoresByPlayer;
                accumulatedScoresByPlayer[fgcc] += __instance._scorePerPlayerThisFrame;
                if (__instance._accumulatedScoresByPlayer[fgcc] >= 1f)
                {
                    Il2CppSystem.Collections.Generic.Dictionary<FallGuysCharacterController, float> accumulatedScoresByPlayer2 = __instance._accumulatedScoresByPlayer;
                    float num = accumulatedScoresByPlayer2[fgcc];
                    accumulatedScoresByPlayer2[fgcc] = num - 1f;
                    CGM._soloScoreManager.AwardSoloPoints(fgcc.NetObject.NetID, 1);
                    AudioManager.PlayOneShot("SFX_OBJ_Airtime_UI_Point", default(Vector3));
                    if (CGM._soloScoreManager.GetSoloScore(fgcc.NetObject.NetID) >= AirTimeObjective.Value)
                    {
                        __instance.End();
                        StateManager.GetState<GameplayState>().DoQual();
                        __instance.PlayerExit(fgcc);
                        AudioManager.PlayOneShot("SFX_OBJ_Airtime_Score_Complete", default(Vector3));
                    }

                }
            }
            return false;
        }

        //[HarmonyPatch(typeof(VolumeZoneTrigger), "OnEnterSensor")]
        //[HarmonyPrefix]
        //static bool OnEnterSensor(VolumeZoneTrigger __instance, COMMON_TriggerVolume.SensorData sensorData)
        //{
        //    if (FallGuyBehaviour._instance.FGMPG != null && FallGuyBehaviour._instance.FGMPG.IsFallGuy)
        //        __instance._volumeZone.PlayerEnter(FallGuyBehaviour._instance.FGMPG.FGCharacterController);
        //    return false;
        //}

        //[HarmonyPatch(typeof(VolumeZoneTrigger), "OnExitSensor")]
        //[HarmonyPrefix]
        //static bool OnExitSensor(VolumeZoneTrigger __instance, COMMON_TriggerVolume.SensorData sensorData)
        //{
        //    if (FallGuyBehaviour._instance.FGMPG != null && FallGuyBehaviour._instance.FGMPG.IsFallGuy)
        //        __instance._volumeZone.PlayerExit(FallGuyBehaviour._instance.FGMPG.FGCharacterController);
        //    return false;
        //}
    }

    public class PrefabSpawnerPatch : FGTBase
    {
        //[HarmonyPatch(typeof(COMMON_PrefabSpawnerBase), "SpawnSelected")]
        //[HarmonyPrefix]
        //static bool SpawnSelected(COMMON_PrefabSpawnerBase __instance, ref bool __result, bool isPreSpawning)
        //{
        //    if (__instance.SelectedEntryIndex < 0)
        //        __result = false;

        //    COMMON_PrefabSpawnerBase.SpawnerEntry spawnerEntry = ((__instance.SelectedEntryIndex >= 0) ? __instance._spawnObjects[__instance.SelectedEntryIndex] : null);
        //    if (spawnerEntry != null) 
        //    __instance.Spawn(spawnerEntry);
        //    __result = spawnerEntry != null;
        //    return false;
        //}

        //[HarmonyPatch(typeof(COMMON_PrefabSpawnerBase), "Start")]
        //[HarmonyPrefix]
        //private static bool Start(COMMON_PrefabSpawnerBase __instance)
        //{
        //    if (__instance._preSpawnMode == PreSpawnMode.Static)
        //        __instance.PreSpawnStaticObjects();
        //    if (__instance._enableBlockageDetection && __instance._blockageTriggerColEvents != null)
        //    {
        //        ColliderEvents blockageTriggerColEvents = __instance._blockageTriggerColEvents;
        //        blockageTriggerColEvents.OnTriggerEnterAction += new Action<Collider>(__instance.HandleBlockageOnTriggerEnter);
        //        ColliderEvents blockageTriggerColEvents2 = __instance._blockageTriggerColEvents;
        //        blockageTriggerColEvents2.OnTriggerExitAction += new Action<Collider>(__instance.HandleBlockageOnTriggerExit);
        //    }
        //    return false;
        //}

        //[HarmonyPatch(typeof(COMMON_PrefabSpawnerBase), "SpawnSpecific")]
        //[HarmonyPrefix]
        //private static bool SpawnSpecific(COMMON_PrefabSpawnerBase __instance, SpawnerEntry entryToSpawn)
        //{
        //    __instance.Spawn(entryToSpawn);
        //    return false;
        //}

        //[HarmonyPatch(typeof(COMMON_PrefabSpawnerBase), "BulkSpawn")]
        //[HarmonyPrefix]
        //private static bool BulkSpawn(COMMON_PrefabSpawnerBase __instance, int itemsToSpawn, Il2CppSystem.Collections.Generic.List<GameObject> spawnPoints, bool isPreSpawning)
        //{
        //    for (int i = 0; i < itemsToSpawn; i++)
        //    {
        //        if (i >= spawnPoints.Count)
        //            break;

        //        SpawnerEntry entry = __instance.GetRandomValidSpawnEntry(isPreSpawning);
        //        __instance.InstantiateObject(entry, spawnPoints[i].transform.position);
        //        __instance._lastEntriesSpawned.Add(entry);
        //        __instance.ClearLastEntriesSpawned();
        //    }
        //    return false;
        //}



    }

    public class AIPatch : FGTBase
    {
        [HarmonyPatch(typeof(NPCController), "Start")]
        [HarmonyPrefix]
        private static bool Start(NPCController __instance)
        {
            __instance.rigidbody = __instance.gameObject.GetComponent<Rigidbody>();
            __instance._defaultPhysicMaterial = __instance._collider.material;
            return false;
        }

        [HarmonyPatch(typeof(NPCController), "FixedUpdate")]
        [HarmonyPrefix]
        private static bool FixedUpdate(NPCController __instance)
        {
            if (__instance._couldMovePreviousFrame != __instance.CanMove && __instance.constrainRotation)
                __instance.rigidbody.constraints = (__instance.CanMove ? RigidbodyConstraints.FreezeRotation : RigidbodyConstraints.None);
            __instance._couldMovePreviousFrame = __instance.CanMove;
            if (__instance.CanMove)
            {
                __instance.Move();
                __instance.Jump();
            }
            __instance._input = NPCController.NPCInput.None;
            return false;
        }

        [HarmonyPatch(typeof(NPCAI), "Start")]
        [HarmonyPrefix]
        private static bool NPCAI_Start(NPCAI __instance)
        {
            __instance.NPC = __instance.gameObject.GetComponent<NPCController>();
            __instance.InitAI();
            return false;
        }

        [HarmonyPatch(typeof(NPCAI), "FixedUpdate")]
        [HarmonyPrefix]
        private static bool NPCAI_FixedUpdate(NPCAI __instance)
        {
            bool canMove = __instance.NPC.CanMove;
            if (canMove)
            {
                foreach (Sensor sensor in __instance._sensors)
                {
                    sensor.UpdateSensor();
                }
                __instance._stateMachine.UpdateState();
            }
            __instance.NPC.TryMove(new NPCController.NPCInput(__instance.targetDirection));
            return false;
        }

        [HarmonyPatch(typeof(COMMON_BullRMIController), "RegisterRemoteMethods")]
        [HarmonyPrefix]
        private static bool BullRMI_RegisterRemoteMethods(COMMON_BullRMIController __instance)
        {
            COMMON_BullBumper bumper = __instance._bumper;

            bumper.OnPlayerCollision += new Action(() => { __instance.SetPlayerCollision(); });

            BullStateCharge charge = __instance._bullAI.stateMachine.chargeState;
            Action SetCancelCharge = () => __instance.SetCancelCharge();
            charge.OnChargeCancel += SetCancelCharge;

            Action SetCharging = () => __instance.SetCharging();
            charge.OnChargeBegin += SetCharging;

            Action SetBraking = () => __instance.SetBraking();
            charge.OnChargeBrake += SetBraking;

            Action OnChargeEnd = () => __instance.SetBraking();
            charge.OnChargeBrake += SetBraking;

            Action SetNormal = () => __instance.SetNormal(__instance._bullAI.stateMachine.chaseState.SpeedOverride);
            charge.OnChargeEnd += SetNormal;

            return false;
        }
    }

    public class TriggerVolumePatch : FGTBase
    {
        //[HarmonyPatch(typeof(COMMON_InfiniteSegmentSpawner), "TriggerNextSegmentSpawn")]
        //[HarmonyPrefix]
        //private static bool TriggerNextSegmentSpawn(COMMON_InfiniteSegmentSpawner __instance, Vector3 localSpawnPosition)
        //{
        //    __instance.PickRandomSegment(out int groupIndex, out int randomObstacleSegmentIndex, out WeightedBufferObstacleGroup randomGroup, out int segmentSelectionBuffer);
        //    __instance.RMI_SpawnNextSegment(groupIndex, randomObstacleSegmentIndex, localSpawnPosition.x, localSpawnPosition.y, localSpawnPosition.z, 1);
        //    __instance.ManageObstacleAndGroupBuffers(groupIndex, randomGroup.SelectionBuffer, randomObstacleSegmentIndex, segmentSelectionBuffer);
        //    return false;
        //}

        [HarmonyPatch(typeof(CollectableTrigger), "OnEnterSensor")]
        [HarmonyPrefix]
        private static bool Start(CollectableTrigger __instance, COMMON_TriggerVolume.SensorData sensorData)
        {
            FGTServiceManager.GetService<StatisticsService>().currentStats.CollectablePickup++;
            try { __instance.Collectable.DoCollected(); } catch { }
            return false;
        }

        [HarmonyPatch(typeof(COMMON_TriggerVolume), "Start")]
        [HarmonyPrefix]
        private static bool Start(COMMON_TriggerVolume __instance)
        {
            return false;
        }

        [HarmonyPatch(typeof(COMMON_TriggerVolume), "OnTriggerEnter")]
        [HarmonyPrefix]
        private static bool OnTriggerEnter(COMMON_TriggerVolume __instance, Collider other)
        {
            if (other.gameObject.GetComponent<FallGuyBehaviour>() != null) 
                __instance.ConsiderEnterSense(other.gameObject);
            return false;
        }

        [HarmonyPatch(typeof(COMMON_TriggerVolume), "OnCollisionEnter")]
        [HarmonyPrefix]
        private static bool OnCollisionEnter(COMMON_TriggerVolume __instance, Collider other)
        {
            if (other.gameObject.GetComponent<FallGuyBehaviour>() != null)
                __instance.ConsiderEnterSense(other.gameObject);
            return false;
        }

        [HarmonyPatch(typeof(COMMON_TriggerVolume), "OnTriggerExit")]
        [HarmonyPrefix]
        private static bool OnTriggerExit(COMMON_TriggerVolume __instance, Collider other)
        {
            __instance.ConsiderExitSense(other.gameObject);
            return false;
        }

        [HarmonyPatch(typeof(COMMON_DestroyVolume), "OnEnterSensor")]
        [HarmonyPrefix]
        private static bool OnEnterSensor(COMMON_DestroyVolume __instance, COMMON_TriggerVolume.SensorData sensorData)
        {
            return false;
        }
    }

    public class PixelPerfectPatch : FGTBase
    {
        [HarmonyPatch(typeof(PixelPerfectInputTileTrigger), "OnEnterSensor")]
        [HarmonyPrefix]
        static bool OnEnterSensor(PixelPerfectInputTileTrigger __instance, COMMON_TriggerVolume.SensorData sensorData)
        {
            bool t = Time.realtimeSinceStartup < __instance._nextInputAllowedTime;
            if (!t)
            {
                __instance._nextInputAllowedTime = Time.realtimeSinceStartup + __instance._board.InputCooldown;
                __instance._board.ReceiveInput(__instance._tile.Index, FallGuyBehaviour._instance.FGMPG);
            }
            return false;
        }

        [HarmonyPatch(typeof(PixelPerfectBoard), "ReceiveInput")]
        [HarmonyPrefix]
        static bool ReceiveInput(PixelPerfectBoard __instance, int buttonIndex, MPGNetObject netObject)
        {
            __instance.ActionInput(buttonIndex);
            if (__instance._displayScreen.Value == __instance._inputScreen.Value)
            {
                __instance.ChangeState(PixelPerfectBoard.PatternComplete.Instance);
                if (CGM._soloScoreManager.GetSoloScore(netObject.NetID) == CGM.GameRules.ScoreTarget)
                    StateManager.GetState<GameplayState>().DoQual();
                else
                    CGM._soloScoreManager.AwardSoloPoints(netObject.NetID, 1);
            }
            return false;
        }
    }

    public class SnowyScrapPatch : FGTBase
    {
        [HarmonyPatch(typeof(SnowyScrapManager), nameof(SnowyScrapManager.Init))]
        [HarmonyPrefix]
        static bool Init(SnowyScrapManager __instance, MPGNetObjectManager netObjectManager, EntityVsGroupManager entityVsGroupManager, GameRules gameRules)
        {
            foreach (COMMON_Scaleable soc in Resources.FindObjectsOfTypeAll<COMMON_Scaleable>())
            {
                soc.SetIterationsToTargetScale(CGM.GameRules.GetScoreTarget());
            }
            return false;
        }

        [HarmonyPatch(typeof(COMMON_Scaleable), "Start")]
        [HarmonyPrefix]
        static bool Start(COMMON_Scaleable __instance)
        {
            __instance.CalculateScalePerIteration();
            return false;
        }

        [HarmonyPatch(typeof(COMMON_Scaleable), "SetIterationsToTargetScale")]
        [HarmonyPrefix]
        static bool SetIterationsToTargetScale(COMMON_Scaleable __instance, int num)
        {
            __instance._iterationsToTargetScale = num;
            __instance.CalculateScalePerIteration();
            return false;
        }

        [HarmonyPatch(typeof(COMMON_Scaleable), "TryScaling")]
        [HarmonyPrefix]
        static bool TryScaling(COMMON_Scaleable __instance, int numIterationsToAdd)
        {
            if (!__instance.IsAtMaximumScale && StateManager.IsInGameplay)
            {
                __instance._iterationsRecorded += numIterationsToAdd;
                Vector3 newScale = __instance.CachedTransform.localScale + __instance._scalePerIteration * numIterationsToAdd;
                __instance.CachedTransform.localScale = Vector3.Min(newScale, __instance.TargetScale);
                if (__instance._destroyOnTargetScale && __instance.IsAtMaximumScale)
                {
                    int id = __instance.gameObject.GetComponent<TeamOwned>().TeamID;
                    Destroy(__instance.GetComponent<MPGNetObject>());
                    if (id == FGBehaviour.PlayerTeamId)
                        StateManager.GetState<GameplayState>().DoQual();
                    else
                        StateManager.GetState<GameplayState>().DoElim();
                }
            }
            return false;
        }

        [HarmonyPatch(typeof(COMMON_SnowMound), "OnTriggerEnter")]
        [HarmonyPrefix]
        static bool OnTriggerEnter(COMMON_SnowMound __instance, Collider other)
        {
            if (StateManager.IsInGameplay && other.gameObject.TryGetComponent<COMMON_Scaleable>(out var soc))
                __instance.HandleCollisionServer(soc);
            return false;
        }

        [HarmonyPatch(typeof(COMMON_SnowMound), "HandleCollisionServer")]
        [HarmonyPrefix]
        static bool HandleCollisionServer(COMMON_SnowMound __instance, COMMON_Scaleable soc)
        {
            __instance._numberOfObjectsCurrentlyOnTile++;
            if (__instance._state != COMMON_SnowMound.State.Off)
            {
                soc.TryScaling(__instance._scoreAwarder.DefaultPointsAmount);
                __instance._scoreAwarder.AwardPoints(soc.gameObject);
                __instance._currentRespawnCountdown = __instance._respawnDelay;
                __instance._state = COMMON_SnowMound.State.Off;
                int team = soc.gameObject.GetComponent<TeamOwned>().TeamID;
                StateManager.GetState<GameplayState>().UpdateTeamsUI(team, StateManager.PTM.GetTeamScore(team));
                __instance.InvokeMethodEverywhere(__instance._indexUpdateComponentsCommon, __instance._state == COMMON_SnowMound.State.On, false);
            }
            return false;
        }

        public class JumpShowdownPlatformsPatch : FGTBase
        {
            [HarmonyPatch(typeof(JumpShowdown_PlatformsController), "Start")]
            [HarmonyPrefix]
            static bool JSPC_Start(JumpShowdown_PlatformsController __instance)
            {
                __instance._state = JumpShowdown_PlatformsController.State.Picking;
                return false;
            }

            [HarmonyPatch(typeof(JumpShowdown_PlatformsController), "ShakeBeforeFall")]
            [HarmonyPrefix]
            static bool JSPC_ShakeBeforeFall(JumpShowdown_PlatformsController __instance)
            {
                var plat = __instance._platforms[__instance._nextPlatformIndex];

                plat.OnPlatformShake();
                __instance._shakeTimer -= Time.deltaTime;
                if (__instance._shakeTimer <= 0f)
                {
                    plat.OnPlatformFall();
                    __instance._platformsFallen++;
                    __instance._state = JumpShowdown_PlatformsController.State.Picking;
                }
                return false;
            }
        }
    }
}

