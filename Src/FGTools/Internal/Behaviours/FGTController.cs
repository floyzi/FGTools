extern alias wle;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using FG.Common;
using FG.Common.CMS;
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
using static FGTools.Config.Config;
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
        }

        void Update()
        {
            CheckFunnyStuff();

            if (GlobalGameStateClient.Instance.GameStateView.GameplayTimeElapsed > Random.Range(25, 40) && HerobrineShouldAppear)
            {
                if (ColorUtility.TryParseHtmlString(CMSLoader.Instance.CMSData.SettingsFallFeed["singleton"].DefaultColor, out var c))
                {
                    CGM._clientPlayerManager.AddPlayer(null, new()
                    {
                        EntityID = 9999,
                        accountID = Guid.NewGuid().ToString(),
                        completedLevel = true,
                        realPlayer = true,
                        platformID = "pc_steam",
                        playerKey = "pc_steam_Herobrine",
                        isParticipant = true,
                        remotePlayerID = 9999,
                        objectNetID = new(9999)
                    }, GlobalGameStateClient.Instance.PlayerProfile.CustomisationSelections);

                    PlatformServices.Instance.PlayerDetailsService.AddOrUpdatePlayer(PlayerDetailsService.NameSource.InGame, "pc_steam", "Herobrine", "", false);

                    Resources.FindObjectsOfTypeAll<FallFeedQualifyEliminateHandler>().FirstOrDefault().HandleQualification(new()
                    {
                        succeeded = true,
                        playerId = 9999,
                    });

                    CGM._qualifiedPlayerCount += 1;
                    CGM._requiredQualifiedPlayerCount += 1;

                    //FGTStateManager.UIM.GetComponentInChildren<GameplayScoringViewModel>().UpdateQualificationProgress();
                }

                HerobrineShouldAppear = false;
            }

            if (PingasShouldAppear)
            {
                if (GlobalGameStateClient.Instance.GameStateView.GameplayTimeElapsed > Random.Range(52, 70) && !FGTServiceManager.GetService<EventService>().ReturnBoolEventValue("MeetPingasAgain"))
                {
                    StartCoroutine(FGTServiceManager.GetService<MediaService>().LoadImage(Launcher.AssetsDir + "pingas.jpg", true).WrapToIl2Cpp());
                    FGTServiceManager.GetService<EventService>().SetEventValue("MeetPingasAgain", true);
                }
            }
        }

        void CheckFunnyStuff()
        {
            if (CGM != null && !CGM.IsShutdown && (SpeedrunMode.Value || CGM.GameRules.IsTimeAttackGameMode))
            {
                if (GlobalGameStateClient.Instance.GameStateView.GameplayTimeElapsed > 1800 && !FGTServiceManager.Instance.GetService<EventService>().ReturnBoolEventValue("SpeedrunnerAlert"))
                {
                    timerViewModel = FGTStateManager.UIM.GetComponentInChildren<InGamePlayingState>().gameObject.GetComponentInChildren<GameplayTimerViewModel>();
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
