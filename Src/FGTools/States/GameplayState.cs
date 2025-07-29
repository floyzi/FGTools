using System;
using System.Linq;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using Events;
using FG.Common;
using FG.Common.Character.MotorSystem;
using FG.Common.CMS;
using FGClient;
using FGClient.FallFeed;
using FGClient.Rendering.XRay;
using FGClient.UI;
using FGClient.UI.Core;
using FGTools.Content;
using FGTools.Internal;
using FGTools.Internal.Behaviours;
using FGTools.Services;
using FGTools.States.Logic;
using Levels.PixelPerfect;
using Levels.ScoreZone;
using SRF;
using UnityEngine;
using UnityEngine.SceneManagement;
using static FG.Common.GameStateMachine;
using static FGTools.Config.ConfigManager;
using static FGTools.Internal.Extensions.FLZ_Extensions;
using static FGTools.Internal.FMODTool;
using static FGTools.Plugin;
using static FGTools.Services.LocalizationService;
using static FGTools.Services.SpeedrunService;
using static FGTools.States.Logic.FGTStateManager;

namespace FGTools.States
{
    public class GameplayState : Logic.FGTState
    {
        internal FGTController Controller;

        public GameplayScoringViewModel[] scoringView;
        public bool qualComplete;
        public bool elimComplete;
        public bool winComplete;
        public bool winResultsPending;
        public bool timeAttackWinResultsPending;
        private float skipIntroHold;
        private bool holdingSkipIntro;

        public override void OnStateSet()
        {
            scoringView = Resources.FindObjectsOfTypeAll<GameplayScoringViewModel>();

            var controller = new GameObject($"{Plugin.DisplayName}_Controller");
            Controller = controller.AddComponent<FGTController>();

            Commands.OnRoundStarts += OnGameplayBegins;
        }

        public void UpdateTeamsUI(int teamId, int score)
        {
            PlayerTeamManager.SetTeamScoreEvent evt = new()
            {
                teamId = teamId,
                score = score
            };
            foreach (GameplayScoringViewModel scoreObj in scoringView)
                try { scoreObj.HandleSetTeamScore(evt); } catch { }
        }

        void OnGameplayBegins()
        {
            StateManager.RoundLoadingAllowed = true;
            if (!StateManager.IsFGC)
                StateManager.HandleFGTState(FGTStateManager.FGTState.GameActive);
            else
                StateManager.HandleFGTState(FGTStateManager.FGTState.FGCGameActive);

            StateManager.HandleFGState(PlayerState.Active);
            if (SpeedrunMode.Value && FGTServiceManager.GetService<SpeedrunService>().SpeedrunState != SpeedrunService.RunState.TempDisabled && FGTServiceManager.GetService<SpeedrunService>().SpeedrunState != SpeedrunService.RunState.TimeAttack)
            {
                FGTServiceManager.GetService<SpeedrunService>().LoadUI();
                FGTServiceManager.GetService<SpeedrunService>().HandleState(SpeedrunService.RunState.Respawned);
            }
            FGBehaviour.FallGuy.GetComponent<Rigidbody>().isKinematic = false;

            if (StateManager.CheckpointModel != null)
                FGBehaviour.spawnpoint = GameObject.Instantiate(StateManager.CheckpointModel);
            else
                FGBehaviour.spawnpoint = GameObject.CreatePrimitive(PrimitiveType.Cube);

            FGBehaviour.spawnpoint.DestroyComponentImmediateIfExists<BoxCollider>();
            FGBehaviour.spawnpoint.GetComponent<MeshRenderer>().enabled = !InvisibleCheckpoint.Value;
            FGBehaviour.spawnpoint.name = "Checkpoint";
            FGBehaviour.spawnpoint.transform.SetPositionAndRotation(FGBehaviour.FallGuy.transform.position, FGBehaviour.FallGuy.transform.rotation);
            FGBehaviour.spawnpoint.SetActive(true);

            if (StateManager.FGTCurrentState != FGTStateManager.FGTState.FGCGameActive)
            {
                foreach (ScoreZoneManager zoneManager in Resources.FindObjectsOfTypeAll<ScoreZoneManager>())
                    zoneManager.ActivateInitialZones();
                foreach (PixelPerfectManager pixelManager in Resources.FindObjectsOfTypeAll<PixelPerfectManager>())
                {
                    pixelManager.Init();
                    pixelManager.BeginGame();
                }
            }

            if (SpeedrunMode.Value && QualLevel.Value == QualType.None)
            {
                DoModal(LocalizedStr("sp_qual_disabled_title"), LocalizedStr("sp_qual_disabled_desc"), UIModalMessage.ModalType.MT_OK_CANCEL, UIModalMessage.OKButtonType.Positive, act: new Action<bool>(act));
                static void act(bool wasok)
                {
                    if (wasok)
                        QualLevel.Value = QualType.LoadRandomRoundAfter;
                }
            }

            string currVer = Plugin.BuildInfo.UI_Version;

            var oS = FGTServiceManager.GetService<OnlineCheckService>();

            if (FGTTargetSettings.UpdateNotification && oS.FGTContent.Config.OutdatedVersions != null && oS.FGTContent.Config.OutdatedVersions.Contains(currVer))
                CreateNotification(LocalizedStr("msg_outdated_ver_title"), LocalizedStr("msg_outdated_ver_short", [oS.FGTContent.Meta.FgtVersion]), FGT_Warning_Color);

            FGTServiceManager.GetService<StatisticsService>().currentStats.TotalRoundsLoaded++;
            FGBehaviour.OnGameplayBegin();
            FGTLog(LogLevel.Info, "OnIntroCountdownEnded", "Gameplay begins...");
        }

        public void DoWin(string timeAttackTime = null, string roundOverTxt = "round_over", bool skipRoundEndedAnim = false)
        {
            if (!winComplete && WinLevel.Value != WinType.None)
            {
                FGTServiceManager.GetService<StatisticsService>().currentStats.WinTotal++;
                FGTServiceManager.GetService<SpeedrunService>().SaveRunTimer(SpeedrunSaveType.Win);
                winComplete = true;
                if (WinLevel.Value == WinType.LoadRandomRoundAfter)
                   StateManager.HandleFGState(PlayerState.Finish);
                if (!skipRoundEndedAnim)
                {
                    RoundEndedScreenViewModel screen = UIManager.Instance.ShowScreen<RoundEndedScreenViewModel>(new ScreenMetaData { OnClosedAction = new Action(winner) });
                    screen.SetText(roundOverTxt);
                    AudioManager.PlayOneShot(AudioManager.EventMasterData.RoundOver);
                }
                else
                    winner();
                void winner()
                {
                    if (!CGM.GameRules.IsTimeAttackGameMode)
                        winResultsPending = true;
                    else
                        timeAttackWinResultsPending = true;
                    if ((!SpeedrunMode.Value || FGTServiceManager.GetService<SpeedrunService>().SpeedrunState == RunState.TempDisabled) && FGTServiceManager.GetService<SpeedrunService>().SpeedrunState == RunState.TimeAttack)
                        WinnerScreenViewModel.Show("winner", true, new Action(afterWinner), CGM.GameRules.IsTimeAttackGameMode ? CGM.GetFocusedTimeAttackScoreEntryUI() : null);
                    else
                    {
                        string txt = CMSLoader.Instance._localisedStrings._localisedStrings["winner"];
                        AddCMSString("sp_win", txt[..^1] + ": " + FGTServiceManager.GetService<SpeedrunService>().ReturnTimerText());
                        WinnerScreenViewModel.Show("sp_win", true, new Action(afterWinner));
                    }
                    var data = Resources.FindObjectsOfTypeAll<BadgeDataViewModel>().FirstOrDefault();
                    data.SetBadgeData(new("gold", CGM._round.Id));
                    data.gameObject.SetActive(true);
                    AudioManager.PlayGameplayEndAudio(true);

                    void afterWinner()
                    {
                        FGTServiceManager.GetService<StatisticsService>().ProcessNewRound(StatisticsService.RoundResult.Win);
                        //if (WinLevel.Value != WinType.OnlyWinScreen)
                        //{
                        //    TryToDespawnFallGuy();
                        //    //if (!offlineMode.Value)
                        //    //{
                        //        GlobalGameStateClient.Instance._gameStateMachine.ReplaceCurrentState(new StateVictoryScreen(GlobalGameStateClient.Instance._gameStateMachine, Singleton<GlobalGameStateClient>.Instance.CreateClientGameStateData(), 102, false, timeAttackTime).Cast<IGameState>());
                        //        Broadcaster.Instance.Broadcast(new OnTransitionToVictoryScreen { RoundResults = CGM._roundResults.AsReadOnly() });
                        //        FMODTool.PlayFMODEvent("MUS_InGame_Win_LP", UnloadParam.UnloadOnNewScene, null);
                        //    //}
                        //    //else
                        //    //    AfterWinPopups(false);
                        //}
                    }
                }
            }
        }

        public override void OnStateExit()
        {
            Commands.OnRoundStarts -= OnGameplayBegins;
        }

        public void FinishGameplay()
        {
            //TryToDespawnFallGuy();
            //if (GlobalGameStateClient.Instance.IsInGameplay)
            //    GlobalGameStateClient.Instance._gameStateMachine.CurrentState.Cast<StateGameInProgress>().Teardown();
        }

        public void TryToDespawnFallGuy()
        {
            //if (StateManager.CGM != null && FGBehaviour.FGMPG != null)
            //{
            //    try
            //    {
            //        StateManager.CGM._untrackPlayerSpawn(FGBehaviour.FGMPG);
            //        StateManager.CGM._netObjectManager.UnspawnNetObject(FGBehaviour.FGMPG.NetID, MPGNetObjectManager.UnspawnGameObjectPolicy.LeaveInScene);
            //        FGBehaviour.FGMPG.ClearNetID();
            //        FGBehaviour.FallGuy.SetActive(false);
            //        FGBehaviour.FGMPG = null;
            //        GlobalGameStateClient.Instance.ResetGame();
            //        NetworkGameData.ClearCurrentGameOptions();
            //    }
            //    catch (Exception e)
            //    {
            //        FGTLog(LogLevel.Error, base.GetType(), $"Despawn failed. {e.Message}");
            //    }
            //}
        }

        public void AfterWinPopups(bool timeAttack, VictoryScreenViewModel player = null)
        {
            if (SpeedrunMode.Value && FGTServiceManager.GetService<SpeedrunService>().SpeedrunState != RunState.TempDisabled && !StateManager.HaveActivePopup && !timeAttack)
            {
                FGTServiceManager.GetService<SpeedrunService>().TriggerSpeedrunContinueModal(true, player);
            }
            else
            {
                if (player != null)
                    player.StopMusicImmediately();
                FGTServiceManager.GetService<RoundLoaderService>().LoadRandomCms();
            }
        }

        public void DoElim(bool doNotAskSpActions = false, bool skipFallFeed = false, bool roundOver = false)
        {
            if (roundOver && ElimLevel.Value == ElimType.None)
                ElimLevel.Value = ElimType.LoadRandomRoundAfter;
            if (!elimComplete && ElimLevel.Value != ElimType.None)
            {
                FGTServiceManager.GetService<StatisticsService>().currentStats.ElimTotal++;
                elimComplete = true;
                if ((!SpeedrunMode.Value || FGTServiceManager.GetService<SpeedrunService>().SpeedrunState == RunState.TempDisabled) || doNotAskSpActions)
                    EliminatedScreenViewModel.Show("eliminated", null, new Action(elimend));
                else
                {
                    string txt = CMSLoader.Instance._localisedStrings._localisedStrings["eliminated"];
                    AddCMSString("sp_elim", txt.Remove(txt.Length - 1) + ": " + FGTServiceManager.GetService<SpeedrunService>().ReturnTimerText());
                    EliminatedScreenViewModel.Show("sp_elim", null, new Action(elimend));
                }
                AudioManager.PlayOneShot(AudioManager.EventMasterData.EliminationMusic);
                if (SpeedrunMode.Value)
                    FGTServiceManager.GetService<SpeedrunService>().SaveRunTimer(SpeedrunSaveType.Elim);
                if (ElimLevel.Value == ElimType.LoadRandomRoundAfter)
                {
                    StateManager.HandleFGState(PlayerState.Finish);
                    EndGameplay(fallFeedAudio: FallFeedManager.FallFeedAudio.Elimination_squad_member, fallFeedSpr: "fallfeed-eliminate", skipFallFeed: skipFallFeed);
                }
                void elimend()
                {
                    FGTServiceManager.GetService<StatisticsService>().ProcessNewRound(StatisticsService.RoundResult.Elim);
                    if (SpeedrunMode.Value &&   FGTServiceManager.GetService<SpeedrunService>().SpeedrunState != RunState.TempDisabled)
                        FGTServiceManager.GetService<SpeedrunService>().HandleState(RunState.Finish);

                    if (StateManager.ShowState == null)
                    {
                        if (ElimLevel.Value == ElimType.LoadRandomRoundAfter)
                            FGTServiceManager.GetService<RoundLoaderService>().LoadRandomCms();
                        else if (roundOver)
                            LeaveMatchPopupManager.Instance.OnClose(true);
                        //if (roundOver)
                        //    elimType.Value = ElimType.None;
                    }
                    else
                        StateManager.ShowState.OnShowProgress();
                }
            }
        }

     

        internal void EndGameplay(SpeedrunSaveType saveType = SpeedrunSaveType.None, bool endMusic = false, FallFeedManager.FallFeedAudio fallFeedAudio = FallFeedManager.FallFeedAudio.None, string fallFeedSpr = null, bool skipFallFeed = false)
        {
            if (StateManager.ExploreState != null)
                StateManager.ExploreState.OnRoundComplete(StateManager.CurrentRound.Id, qualComplete);
            if (!SpeedrunMode.Value)
            FMODTool.UnloadAllLoadedBanks(FMODTool.UnloadParam.Default);
            RewiredManager.Instance.DisableMap(0, 0);
            StateManager.UIM.LocalPlayerProgressed();
            StateManager.HandleFGState(PlayerState.Finish);
            StateManager.FGTCurrentState = FGTStateManager.FGTState.GameEnded;
            XRayUtils.RemoveXRayControllerForCharacter(FGBehaviour.FGCC);
            CGM._netObjectManager.UnspawnNetObject(FGBehaviour.FGMPG.NetID, MPGNetObjectManager.UnspawnGameObjectPolicy.Destroy);

            if (fallFeedAudio != FallFeedManager.FallFeedAudio.None && !skipFallFeed)
                Resources.FindObjectsOfTypeAll<FallFeedQualifyEliminateHandler>().FirstOrDefault().BroadCastMessage("0", string.Format($"<color=#{CMSLoader.Instance._fallFeedData.teamColor}>{GlobalGameStateClient.Instance.PlayerProfile.PlatformAccountName}</color>".ToUpper()), fallFeedSpr, fallFeedAudio, default);
            if (SpeedrunMode.Value)
                FGTServiceManager.GetService<SpeedrunService>().SaveRunTimer(saveType);
            
            MotorAgent motorAgent = FGBehaviour.GetComponent<MotorAgent>();
            motorAgent.OnDestroy();
            Resources.FindObjectsOfTypeAll<PhysicsSimulator>().FirstOrDefault().RunPhysicsAutomatically = false;
         
            var felloffguy = GameObject.Find(FGBehaviour.name + "/Character/GEO");
            if (felloffguy != null)
                felloffguy.SetActive(false);

            if (endMusic && CGM != null)
                FMODTool.EndFmod(CGM._musicInstance._eventInstance, FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
        }

        public void DoQual(bool doNotAskSpActions = false, bool skipFallFeed = false, bool roundOver = false)
        {
            if (roundOver && QualLevel.Value == QualType.None)
                QualLevel.Value = QualType.LoadRandomRoundAfter;
            if (!qualComplete && QualLevel.Value != QualType.None && !CGM.GameRules.IsTimeAttackGameMode)
            {
                FGTServiceManager.GetService<StatisticsService>().currentStats.QualTotal++;
                string qualSpr = "fallfeed-race-first";
                var aud = FallFeedManager.FallFeedAudio.Qualification_1st;
                qualComplete = true;
                bool allowRand = QualLevel.Value == QualType.LoadRandomRoundAfter;
                if ((!SpeedrunMode.Value || FGTServiceManager.GetService<SpeedrunService>().SpeedrunState == RunState.TempDisabled) || doNotAskSpActions)
                    QualifiedScreenViewModel.Show("qualified", new Action(OnClosed), null);
                else
                {
                    string txt = CMSLoader.Instance._localisedStrings._localisedStrings["qualified"];
                    AddCMSString("sp_qual", txt[..^1] + ": " + FGTServiceManager.GetService<SpeedrunService>().ReturnTimerText());
                    QualifiedScreenViewModel.Show("sp_qual", new Action(OnClosed), null);
                    if (CGM._round.Id.Contains("drum") && DiveSens.Value >= 100 && FGTServiceManager.GetService<SpeedrunService>().ReturnCurrentTime() <= 40 && !FGTServiceManager.GetService<EventService>().ReturnBoolEventValue("MeetPingas"))
                    {
                        Instance.StartCoroutine(FGTServiceManager.GetService<MediaService>().LoadImage(Plugin.AssetsDir + "pingas.jpg").WrapToIl2Cpp());
                        FGTServiceManager.GetService<EventService>().SetEventValue("MeetPingas", true);
                    }
                }
                var data = Resources.FindObjectsOfTypeAll<BadgeDataViewModel>().FirstOrDefault();
                if (CGM.QualifiedPlayerCount == 0)
                    data.SetBadgeData(new("gold", CGM._round.Id));
                else
                {
                    data.SetBadgeData(new("silver", CGM._round.Id));
                    qualSpr = "fallfeed-race";
                    aud = FallFeedManager.FallFeedAudio.Qualification_squad_member;
                }
                data.gameObject.SetActive(true);
                AudioManager.PlayGameplayEndAudio(true);
                if (SpeedrunMode.Value)
                {
                    FGTServiceManager.GetService<SpeedrunService>().TriggerTimer(false);
                    if (allowRand)
                        EndGameplay(SpeedrunSaveType.Qual, fallFeedAudio: aud, fallFeedSpr: qualSpr, skipFallFeed: skipFallFeed);
                }
                else if (allowRand)
                    EndGameplay(fallFeedAudio: aud, fallFeedSpr: qualSpr, skipFallFeed: skipFallFeed);

                void OnClosed()
                {
                    FGTServiceManager.GetService<StatisticsService>().ProcessNewRound(StatisticsService.RoundResult.Qual);
                    if (StateManager.ShowState == null)
                    {
                        if (SpeedrunMode.Value && FGTServiceManager.GetService<SpeedrunService>().SpeedrunState != RunState.TempDisabled && !doNotAskSpActions)
                        {
                            FGTServiceManager.GetService<SpeedrunService>().HandleState(RunState.Finish);
                            FGTServiceManager.GetService<SpeedrunService>().TriggerSpeedrunContinueModal();
                        }

                        if (allowRand && (!SpeedrunMode.Value || FGTServiceManager.GetService<SpeedrunService>().SpeedrunState == RunState.TempDisabled) || doNotAskSpActions)
                            FGTServiceManager.GetService<RoundLoaderService>().LoadRandomCms();
                    }
                    else
                        StateManager.ShowState.OnShowProgress();
                }
            }
        }

        public void RoundIntroGUI()
        {
            float offsetX = Screen.width - 210f;
            float offsetY = 25f;

            if (StateManager.FGTCurrentState == FGTStateManager.FGTState.RoundIntro && StateManager.InternalState.LoaderUIToggle)
            {
                string label = $"{LocalizedStr("intro_skip_msg", [SkipIntroHotkey.Value, SkipIntroTime.Value])}";
                if (holdingSkipIntro)
                    label = $"{skipIntroHold:F1} / {SkipIntroTime.Value}";

                var labelSize = GUI.skin.label.CalcSize(new(label)).x + 2000;

                GUI.Box(new Rect(offsetX + 10, offsetY - 30, labelSize, 30f), "");

                GUI.Label(new Rect(offsetX + 15, offsetY - 25, labelSize, 30f), label);
            }
        }

        public override void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
        }

        public override void UpdateState()
        {
            if (StateManager.FGTCurrentState == FGTStateManager.FGTState.RoundIntro)
            {
                if (Input.GetKey(SkipIntroHotkey.Value))
                {
                    skipIntroHold += Time.deltaTime;

                    if (!holdingSkipIntro)
                    {
                        holdingSkipIntro = true;
                        skipIntroHold = 0f;
                    }

                    if (Input.GetKeyUp(SkipIntroHotkey.Value))
                    {
                        holdingSkipIntro = false;
                        skipIntroHold = 0f;
                    }

                    else if (skipIntroHold >= SkipIntroTime.Value)
                    {
                        FGTServiceManager.GetService<RoundLoaderService>().RoundCamera.StopIntroCameras();
                        CGM.FinishPreparationPhase();
                        holdingSkipIntro = false;
                        skipIntroHold = 0f;
                    }
                }
                else
                {
                    holdingSkipIntro = false;
                    skipIntroHold = 0f;
                }
            }
        }

        public override void DisplayGUI()
        {
            RoundIntroGUI();
        }
    }
}
