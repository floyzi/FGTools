extern alias wle;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using FG.Common;
using FGClient;
using FGClient.FallFeed;
using FGClient.UI;
using FGClient.UI.Core;
using FGTools.HarmonyPatches;
using FGTools.Services;
using FGTools.Services.Logic;
using FGTools.States;
using FGTools.States.Logic;
using Levels.DoorDash;
using Levels.Obstacles;
using Levels.Progression;
using Levels.TimeAttack;
using Levels.TipToe;
using Mediatonic.Tools.Utils;
using Rewired;
using System;
using System.Linq;
using UnityEngine;
using static FGClient.FallFeed.FallFeedManager;
using static FGTools.Config.ConfigManager;
using static FGTools.Internal.Extensions.FLZ_Extensions;
using static FGTools.Services.LocalizationService;
using static FGTools.UI.ReadyPopups;
using Random = UnityEngine.Random;
using Vector3 = UnityEngine.Vector3;

namespace FGTools.Internal.Behaviours
{
    internal class FGTController : ToolsBehaviour
    {
        public static TimeAttackManager TimeAttackManager;
        public static Player rewired;
        public static CheckpointManager CheckpointManager;
        GameplayState CurrentGPState;

        public bool HerobrineShouldAppear;
        public bool PingasShouldAppear;

        bool roundEnded = false;
        GameplayTimerViewModel timerViewModel;
        bool needToUpdateTimer = false;
        float timeToAlert = 45;
        public bool AllowRoundEnd
        {
            get
            {
                return FGTServiceManager.Instance.GetService<RoundOptionsService>().ReturnLatestOptions().TimeLimit;
            }
        }

        public bool OverrideRoundEnd
        {
            get
            {
                return !AllowRoundEnd && !CGM.IsShutdown && (StateManager.ExploreState != null || StateManager.ShowState != null) && (CGM.GameRules.IsFinalRound || CGM.GameRules.IsSurvivalRound) && !CGM.GameRules.IsRaceRound;
            }
        }

        public void Awake()
        {
            CurrentGPState = StateManager.GetState<GameplayState>();

            TimeAttackManager = Resources.FindObjectsOfTypeAll<TimeAttackManager>().FirstOrDefault();
            rewired = Resources.FindObjectsOfTypeAll<InGamePlayingState>().FirstOrDefault()._rewiredPlayer;
            CheckpointManager = Resources.FindObjectsOfTypeAll<CheckpointManager>().FirstOrDefault();

            if (!CGM.GameRules.IsFinalRound && CGM.GameRules.IsRaceRound)
            {
                if (Random.value < ToRandom(0.1f))
                    HerobrineShouldAppear = true;
            }

            if (!HerobrineShouldAppear)
            {
                if (Random.value < ToRandom(0.3f) && !FGTServiceManager.GetService<EventService>().ReturnBoolEventValue("MeetPingasAgain"))
                    PingasShouldAppear = true;
            }

            FixObstacles();
        }

        void Update()
        {
            CheckRoundEnd();
            CheckFunnyStuff();

            if (GlobalGameStateClient.Instance.GameStateView.GameplayTimeElapsed > Random.Range(25, 40) && HerobrineShouldAppear)
            {
                Resources.FindObjectsOfTypeAll<FallFeedQualifyEliminateHandler>().FirstOrDefault().BroadCastMessage("0", "Herobrine", "fallfeed-race-first", FallFeedAudio.Qualification_1st, default);

                CGM._qualifiedPlayerCount += 1;
                CGM._requiredQualifiedPlayerCount += 1;
                StateManager.UIM.GetComponentInChildren<GameplayScoringViewModel>().UpdateQualificationProgress();
                HerobrineShouldAppear = false;
            }

            if (PingasShouldAppear)
            {
                if (GlobalGameStateClient.Instance.GameStateView.GameplayTimeElapsed > Random.Range(52, 70) && !FGTServiceManager.GetService<EventService>().ReturnBoolEventValue("MeetPingasAgain"))
                {
                    StartCoroutine(FGTServiceManager.GetService<MediaService>().LoadImage(Plugin.AssetsDir + "pingas.jpg", true).WrapToIl2Cpp());
                    FGTServiceManager.GetService<EventService>().SetEventValue("MeetPingasAgain", true);
                }
            }
        }

        public void FixObstacles()
        {
            return;
            try
            {
                FGTLog(LogLevel.Info, GetType(), "Fixing obstacles");

                var ttpc = Resources.FindObjectsOfTypeAll<TipToe_PlatformController>().FirstOrDefault();
                if (ttpc != null)
                    gameObject.AddComponent<LocalPLatformShake>().ttpc = ttpc;

                var bbam = Resources.FindObjectsOfTypeAll<ButtonBasherArenaManager>().FirstOrDefault();
                if (bbam != null)
                    gameObject.AddComponent<FFAButtonManager>();

                foreach (COMMON_FakeDoorRandomiser cfdr in FindObjectsOfType<COMMON_FakeDoorRandomiser>())
                {
                    cfdr.InitializeServerSideData();
                    cfdr.CreateBreakableDoors();
                }

                foreach (COMMON_RespawningTile respawningTile in FindObjectsOfType<COMMON_RespawningTile>())
                {
                    respawningTile.gameObject.transform.Find("Trigger").gameObject.AddComponent<RespawnTileController>();
                    RespawnTileController fix = respawningTile.gameObject.transform.Find("Trigger").gameObject.GetComponent<RespawnTileController>();
                    fix.enabled = true;
                    fix.allowDespawnAtStep = respawningTile._despawningStrategy == DespawnPlatformStrategy.DespawnUponContactOrExplosion;
                    fix.tile = respawningTile;
                }

                foreach (COMMON_KillZone killzone in Resources.FindObjectsOfTypeAll<COMMON_KillZone>())
                {
                    if (killzone.gameObject != null)
                    {
                        //bool validZone = killzone.gameObject.transform.GetChild(0).GetChildCount() > 0;
                        //if (validZone)
                        //    killzone.gameObject.transform.GetChild(0).transform.GetChild(0).gameObject.AddComponent<KillZone>();
                        killzone.gameObject.AddComponent<KillZone>();
                    }
                }

                GameObject[] possibleTargets;
                var mpgNetObjects = Resources.FindObjectsOfTypeAll<MPGNetObjectBase>().Select(obj => obj.gameObject);
                var movableObjects = Resources.FindObjectsOfTypeAll<wle.LevelEditorMovableObject>().Select(obj => obj.gameObject);
                possibleTargets = mpgNetObjects.Concat(movableObjects).ToArray();

                foreach (GameObject obj in possibleTargets)
                {
                    if (obj.GetComponent<OfflineGrabTargetID>() == null)
                    {
                        OfflineGrabTargetID offlineGrabTargetID = obj.gameObject.AddComponent<OfflineGrabTargetID>();
                        offlineGrabTargetID._hashID = (uint)Random.Range(10000, 99999);
                        offlineGrabTargetID.Type = OfflineGrabTargetID.OfflineGrabTargetIDType.Grab | OfflineGrabTargetID.OfflineGrabTargetIDType.Mantle;
                    }
                }


                foreach (PlayerRatioedBulkItemSpawner shit in Resources.FindObjectsOfTypeAll<PlayerRatioedBulkItemSpawner>())
                {
                    if (shit.ItemParents.Count > 0)
                    {
                        foreach (Transform trans in shit.ItemParents)
                        {
                            int childCount = trans.childCount;
                            for (int i = 0; i < childCount; i++)
                            {
                                Vector3 newPos = trans.GetChild(i).transform.position;
                                GameObject newSpawn = Instantiate(shit.ItemPrefab);
                                newSpawn.gameObject.transform.position = newPos;
                            }
                        }
                    }
                    else
                    {
                        int childCount = shit.ItemParent.transform.childCount;
                        for (int i = 0; i < childCount; i++)
                        {
                            Vector3 newPos = shit.ItemParent.transform.GetChild(i).transform.position;
                            GameObject newSpawn = Instantiate(shit.ItemPrefab);
                            newSpawn.gameObject.transform.position = newPos;
                        }
                    }
                }

                if (CGM.GameRules.IsTimeAttackGameMode)
                {
                    foreach (TimeAttackManager tam in Resources.FindObjectsOfTypeAll<TimeAttackManager>())
                        tam.Init(CGM._netObjectManager, CGM.EntityVsGroupManager, CGM.GameRules);

                    AttackOfTheTime.SetupTimeBubbles();
                }
                else
                {
                    foreach (TimeAttackManager tam in Resources.FindObjectsOfTypeAll<TimeAttackManager>())
                        tam.enabled = false;
                }

                if (StateManager.IsFGC)
                {
                    foreach (wle.LevelEditorTriggerZoneActiveBase zone in Resources.FindObjectsOfTypeAll<wle.LevelEditorTriggerZoneActiveBase>())
                    {
                        // zone._isActive = zone.startsActive;
                        zone.Awake();
                    }
                }
                FGTLog(LogLevel.Info, GetType(), "Complete");
            }
            catch (Exception E)
            {
                FGTLog(LogLevel.Error, GetType(), E.Message);
                CreateNotification(LocalizedStr("failed_title"), $"{LocalizedStr("gui_fix_obstacles_error")}\n\n\"{E.Message} | {E.StackTrace}\"\n\n{LocalizedStr("gui_error_msg")} {LocalizedStr("gui_error_0")}", FGT_Info_Color);
            }
        }

        void CheckRoundEnd()
        {
            return;

            if (CGM != null && CGM.IsTimerEnded() && !roundEnded && (OverrideRoundEnd || AllowRoundEnd))
            {
                CurrentGPState.EndGameplay();
                if (!CGM.GameRules.IsTimeAttackGameMode)
                {
                    RoundEndedScreenViewModel screen = UIManager.Instance.ShowScreen<RoundEndedScreenViewModel>(new ScreenMetaData { OnClosedAction = new Action(() =>
                    {
                        if (CGM.GameRules.IsSurvivalRound)
                        {
                            if (!CGM.GameRules.IsFinalRound)
                                CurrentGPState.DoQual(true, true, true);
                            else
                                CurrentGPState.DoWin(skipRoundEndedAnim: true);
                        }
                        else
                            CurrentGPState.DoElim(true, true, true);
                    })});

                    screen.SetText("round_over");
                    AudioManager.PlayOneShot(AudioManager.EventMasterData.RoundOver);
                }
                else
                {
                    if (AttackOfTheTime.PlayerStats.GetSortedLapTimes != null)
                        CurrentGPState.DoWin(UIUtils.CreateTimeText(AttackOfTheTime.PlayerStats.GetSortedLapTimes[0]), "timeattack_time_up");
                    else
                    {
                        RoundEndedScreenViewModel.Show(new Action(() => CurrentGPState.DoElim(true, true)), "timeattack_time_up");
                        AudioManager.PlayOneShot(AudioManager.EventMasterData.RoundOver);
                    }
                }
                roundEnded = true;
            }
        }

        void CheckFunnyStuff()
        {
            if (CGM != null && (SpeedrunMode.Value || CGM.GameRules.IsTimeAttackGameMode))
            {
                if (GlobalGameStateClient.Instance.GameStateView.GameplayTimeElapsed > 1800 && !FGTServiceManager.Instance.GetService<EventService>().ReturnBoolEventValue("SpeedrunnerAlert"))
                {
                    timerViewModel = StateManager.UIM.GetComponentInChildren<InGamePlayingState>().gameObject.GetComponentInChildren<GameplayTimerViewModel>();
                    CreateNotification(LocalizedStr("gui_speedrunner_popup_alert"), LocalizedStr("gui_speedrunner_popup_alert_1"), FGT_Info_Color);
                    timerViewModel.RaisePropertyChanged("ShouldShowSmallTimeRemaining", true, false);
                    FGTServiceManager.Instance.GetService<EventService>().SetEventValue("SpeedrunnerAlert", true);
                    needToUpdateTimer = true;
                }

                if (needToUpdateTimer)
                {
                    float time = timeToAlert -= Time.deltaTime;
                    timerViewModel.CreateTimeRemaining(time);
                    if (time <= 0)
                    {
                        timerViewModel.RaisePropertyChanged("ShouldShowSmallTimeRemaining", false, false);
                        AlertSpeedrunner();
                        needToUpdateTimer = false;
                    }
                }
            }
        }


        float ToRandom(float val)
        {
            return val / 100;
        }
    }

}
