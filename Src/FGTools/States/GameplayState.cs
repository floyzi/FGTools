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
using Levels.Progression;
using Levels.ScoreZone;
using SRF;
using System;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using static FG.Common.GameStateMachine;
using static FGClient.FallFeed.FallFeedManager;
using static FGTools.Config.Config;
using static FGTools.Internal.Extensions.FLZ_Extensions;
using static FGTools.Internal.FMODTool;
using static FGTools.Launcher;
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
        public GameObject Spawnpoint;

        public override void OnStateSet()
        {
            scoringView = Resources.FindObjectsOfTypeAll<GameplayScoringViewModel>();

            var controller = new GameObject($"{Launcher.DisplayName}_Controller");
            Controller = controller.AddComponent<FGTController>();

            GameActions.OnRoundStarts += OnGameplayBegins;
            GameActions.OnCheckpointReached += OnCheckpoint;
        }

        void OnCheckpoint(MPGNetObject mpg, CheckpointZone zone)
        {
            if (!mpg.IsFallGuy || !mpg.FGCharacterController.IsLocalPlayer || !FGTServiceManager.GetService<SpeedrunService>().IsSepeedrunsDisabled)
                return;

            zone.GetNextSpawnPositionAndRotation(out var pos, out var rot);
            Spawnpoint.transform.SetPositionAndRotation(pos, rot);
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
                StateManager.HandleFGTState(ToolsState.GameActive);
            else
                StateManager.HandleFGTState(ToolsState.FGCGameActive);

            StateManager.HandleFGState(PlayerState.Active);

            FGBehaviour.FGCC.RigidBody.isKinematic = false;

            if (Spawnpoint == null)
            {
                if (StateManager.CheckpointModel != null)
                    Spawnpoint = GameObject.Instantiate(StateManager.CheckpointModel);
                else
                    Spawnpoint = GameObject.CreatePrimitive(PrimitiveType.Cube);

                Spawnpoint.DestroyComponentImmediateIfExists<BoxCollider>();
                Spawnpoint.GetComponent<MeshRenderer>().enabled = !InvisibleCheckpoint.Value;
                Spawnpoint.name = "Checkpoint";
                Spawnpoint.transform.SetPositionAndRotation(FGBehaviour.transform.position, FGBehaviour.transform.rotation);
                Spawnpoint.SetActive(true);

                if (SpeedrunMode.Value && !FGTServiceManager.GetService<SpeedrunService>().IsSepeedrunsDisabled)
                    FGTServiceManager.GetService<SpeedrunService>().PrepareForGameplay();
            }

            if (StateManager.FGTCurrentState != FGTStateManager.ToolsState.FGCGameActive)
            {
                //foreach (ScoreZoneManager zoneManager in Resources.FindObjectsOfTypeAll<ScoreZoneManager>())
                //    zoneManager.ActivateInitialZones();
                //foreach (PixelPerfectManager pixelManager in Resources.FindObjectsOfTypeAll<PixelPerfectManager>())
                //{
                //    pixelManager.Init();
                //    pixelManager.BeginGame();
                //}
            }

            if (SpeedrunMode.Value && QualLevel.Value == QualType.None)
            {
                DoModal(new(LocalizedStr("sp_qual_disabled_title"), LocalizedStr("sp_qual_disabled_desc"), UIModalMessage.ModalType.MT_OK_CANCEL, UIModalMessage.OKButtonType.Positive, onClick: new Action<bool>(wasok => {
                    if (wasok)
                        QualLevel.Value = QualType.LoadRandomRoundAfter;
                })));
            }

            string currVer = FGToolsBuildDetails.Version;

            var oS = FGTServiceManager.GetService<OnlineCheckService>();

            if (FGTTargetSettings.UpdateNotification && oS.FGTContent.Config.OutdatedVersions != null && oS.FGTContent.Config.OutdatedVersions.Contains(currVer))
                CreateNotification(LocalizedStr("msg_outdated_ver_title"), LocalizedStr("msg_outdated_ver_short", [oS.FGTContent.Meta.FgtVersion]), FGT_Warning_Color);

            FGTServiceManager.GetService<StatisticsService>().CurrentStats.TotalRoundsLoaded++;
            FGBehaviour.OnGameplayBegin();

            FGTLog(LogLevel.Info, "OnIntroCountdownEnded", "Gameplay begins...");
        }

        public override void OnStateExit()
        {
            if (Spawnpoint != null) GameObject.DestroyImmediate(Spawnpoint);
            GameActions.OnRoundStarts -= OnGameplayBegins;
            GameActions.OnCheckpointReached -= OnCheckpoint;
        }

        public void RoundIntroGUI()
        {
            if (!StateManager.InternalState.LoaderUIToggle || !LocalServerService.IsUserAloneAndHost || !StateManager.IsIntroPlaying)
                return;

            var label = $"{LocalizedStr("intro_skip_msg", [SkipIntroHotkey.Value, SkipIntroTime.Value])}";
            if (holdingSkipIntro)
                label = $"{skipIntroHold:F1} / {SkipIntroTime.Value}";

            var disp = $"<b>{label}</b>".ToUpper();

            var lbSize = GUI.skin.label.CalcSize(new GUIContent(disp));
            var boxWidth = lbSize.x + 20f;

            GUI.Box(new Rect(Screen.width - boxWidth + 5f, 0f, boxWidth, 30f), "");
            GUI.Label(new Rect(Screen.width - boxWidth + 10f, 5f, lbSize.x, lbSize.y), disp);
        }

        public override void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
        }

        public override void UpdateState()
        {
            if (!StateManager.IsIntroPlaying || !LocalServerService.IsUserAloneAndHost)
                return;

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
                    CGM.SetReady(PlayerReadinessState.ReadyToPlay);
                    StateManager.HandleFGTState(ToolsState.IntroComplete);
                }
            }
            else
            {
                holdingSkipIntro = false;
                skipIntroHold = 0f;
            }
        }

        public override void DisplayGUI()
        {
            RoundIntroGUI();
        }
    }
}
