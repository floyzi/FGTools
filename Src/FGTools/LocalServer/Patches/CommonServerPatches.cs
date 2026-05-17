extern alias wle;
using FG.Common;
using FG.Common.CMS;
using FG.Common.LODs;
using FG.Common.Messages;
using FGClient;
using FGClient.UI;
using FGTools.Config;
using FGTools.Internal;
using FGTools.Internal.Behaviours;
using FGTools.Internal.Extensions;
using FGTools.LocalServer.CustomMessages.Logic;
using FGTools.LocalServer.Implementations;
using FGTools.Services;
using FGTools.States.Logic;
using HarmonyLib;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Levels.HexARing;
using Levels.HexSnake;
using Levels.Obstacles;
using Levels.Progression;
using Mediatonic.Networking;
using Rewired;
using SRF;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using static FGTools.Internal.Extensions.FLZ_Extensions;
using static FGTools.LocalServer.CustomMessages.Logic.CustomMessageManager;
using static FGTools.Services.SpeedrunService;
using static Il2CppMono.Security.X509.X520;

namespace FGTools.LocalServer.Patches
{
    /// <summary>
    /// Patches that should run on both client and the server during game
    /// </summary>
    internal class CommonServerPatches : FGTBase
    {
        [HarmonyPatch(typeof(GlobalGameStateClient), nameof(GlobalGameStateClient.InitializeConnectionToServer)), HarmonyPostfix]
        static void OnClientConnected(GlobalGameStateClient __instance)
        {
            GameActions.OnConnectedToServer?.Invoke();
        }

        [HarmonyPatch(typeof(ClientNetworkMessageProcessor), nameof(ClientNetworkMessageProcessor.processMessage)), HarmonyPrefix]
        static bool processMessage(ClientNetworkMessageProcessor __instance, GameConnection sender, GameMessageBase msg)
        {
            GameActions.OnReceivedMessage?.Invoke(msg.getGameMessageType());
            return true;
        }


        [HarmonyPatch(typeof(UnityNetworkingGameConnection), nameof(UnityNetworkingGameConnection.SendReliable)), HarmonyPrefix]
        static bool SendReliable(UnityNetworkingGameConnection __instance, Il2CppSystem.ArraySegment<byte> message)
        {
            bool isCustom = message._array[0] >= (byte)(FLZ_CustomMessage)Enum.GetValues(typeof(FLZ_CustomMessage)).GetValue(0);

            if (isCustom && __instance._connectionState != GameConnection.ConnectionState.Disconnecting)
                __instance._unityConnection.Send(message, 0);

            return !isCustom;
        }

        [HarmonyPatch(typeof(NetworkConnection), nameof(NetworkConnection.SetHandlers)), HarmonyPostfix]
        static void SetHandlers(NetworkConnection __instance, NetworkMessageHandlers handlers)
        {
            foreach (var obj in Enum.GetValues(typeof(FLZ_CustomMessage)))
            {
                FGTLog(BepInEx.Logging.LogLevel.Debug, "CONN - " + __instance.connectionId, $"Reserving handle [{(byte)(FLZ_CustomMessage)obj}] for custom messages");
                handlers.RegisterHandler((byte)(FLZ_CustomMessage)obj, DelegateSupport.ConvertDelegate<NetworkMessageDelegate>(LocalServerService.CustomMessageManager.OnCustomMessageReceived));
            }
        }

        [HarmonyPatch(typeof(UnityNetworkingGameConnection), nameof(UnityNetworkingGameConnection.SendUnreliable)), HarmonyPrefix]
        static bool SendUnreliable(UnityNetworkingGameConnection __instance, Il2CppSystem.ArraySegment<byte> message)
        {
            bool isCustom = message._array[0] >= (byte)(FLZ_CustomMessage)Enum.GetValues(typeof(FLZ_CustomMessage)).GetValue(0);

            if (isCustom && __instance._connectionState != GameConnection.ConnectionState.Disconnecting)
                __instance._unityConnection.Send(message, 1);

            return !isCustom;
        }

        [HarmonyPatch(typeof(NetworkMessageStats), nameof(NetworkMessageStats.GameMessageReceived)), HarmonyPrefix]
        static bool GameMessageReceived(NetworkMessageStats __instance, Il2CppSystem.ArraySegment<byte> msgTypeCollection, int connectionID)
        {
            return !(msgTypeCollection._array[0] >= (byte)(FLZ_CustomMessage)Enum.GetValues(typeof(FLZ_CustomMessage)).GetValue(0));
        }

        [HarmonyPatch(typeof(ClientGameManager), nameof(ClientGameManager.GameLevelLoaded)), HarmonyPostfix]
        static void GameLevelLoaded(MPGNetObjectManager __instance, string ugcLevelHash)
        {
            GameActions.OnRoundLoaded?.Invoke();
        }

        //[HarmonyPatch(typeof(CheckpointManager), nameof(CheckpointManager.Start)), HarmonyPostfix]
        //static void Start(CheckpointManager __instance)
        //{
        //    uint i = 0;
        //    foreach (var zone in __instance._checkpointZones)
        //    {
        //        zone.uniqueId = i++;
        //    }
        //}

        [HarmonyPatch(typeof(CheckpointManager), nameof(CheckpointManager.HandleCheckpointReached)), HarmonyPostfix]
        static void HandleCheckpointReached(CheckpointManager __instance, CheckpointZone cpz, MPGNetObject mpgno, bool __result)
        {
            if (__instance.NetIDToCheckpointMap.TryGetValue(mpgno.NetID, out var checkpointMap) && checkpointMap == cpz.UniqueId || !__result)
                return;

            __instance._netIDToCheckpointMap[mpgno.NetID] = cpz.UniqueId;
            GameActions.OnCheckpointReached?.Invoke(mpgno, cpz);
        }

        [HarmonyPatch(typeof(ClientGameManager), nameof(ClientGameManager.HandleLocalPlayerLapComplete)), HarmonyPostfix]
        static void HandleLocalPlayerLapComplete(ClientGameManager __instance, LocalPlayerLapCompleteEvent evt)
        {
            GameActions.OnLapComplete?.Invoke();
        }

        [HarmonyPatch(typeof(SkipRoundButton), nameof(SkipRoundButton.OnEnable)), HarmonyPrefix]
        static bool OnEnable(SkipRoundButton __instance)
        {
            __instance._rewiredPlayer = ReInput.players.GetPlayer(0);
            __instance._rewiredActions = new([199]);
            return false;
        }

        [HarmonyPatch(typeof(GameplayInstructionsViewModel), nameof(GameplayInstructionsViewModel.ButtonResetTimeAttackLap)), HarmonyPrefix]
        static bool ButtonResetTimeAttackLap(GameplayInstructionsViewModel __instance)
        {
            var sp = FGTServiceManager.GetService<SpeedrunService>();
            if (!ConfigManager.SpeedrunMode.Value || sp.IsSepeedrunsDisabled)
                return true;

            ServerGameStateActions.Instance.RespawnParticipant(CGM.GetNetObjectByID(CGM.GetFocusedNetId()).FGCharacterController);
            sp.NewRun();
            return false;
        }

        [HarmonyPatch(typeof(GameplaySpectatorUltimatePartyFlowViewModel), nameof(GameplaySpectatorUltimatePartyFlowViewModel.BeginMatchmakingAfterCountdown)), HarmonyPrefix]
        static bool BeginMatchmakingAfterCountdown(BannersDefault __instance, int seconds)
        {
            FGTServiceManager.GetService<StatisticsService>().ProcessNewRound(StatisticsService.RoundResult.Qual);

            if (StateManager.IsPlayingExplore)
            {
                StateManager.ExploreState.RequestNewRound();
            }
            else
            {
                FGTLog(BepInEx.Logging.LogLevel.Error, "BeginMatchmakingAfterCountdown", "Complete message was seen without an explore state.");
                FLZ_Extensions.ForceExit();
            }
            return false;
        }

        [HarmonyPatch(typeof(BannersDefault), nameof(BannersDefault.CreateMessageComplete)), HarmonyPrefix]
        static bool CreateMessageComplete(BannersDefault __instance)
        {
            AudioManager.PlayGameplayEndAudio(true);
            __instance.State = BannersDefault.BannerActive.Complete;
            var speedrun = ConfigManager.SpeedrunMode.Value && !FGTServiceManager.GetService<SpeedrunService>().IsSepeedrunsDisabled;

            if (speedrun)
            {
                string txt = CMSLoader.Instance._localisedStrings._localisedStrings["complete"];
                AddCMSString("sp_qual_comp", txt[..^1] + ": " + FGTServiceManager.GetService<SpeedrunService>().ReturnTimerText());
                FGTServiceManager.GetService<SpeedrunService>().HandleState(RunState.Finish);
            }

            WinnerScreenViewModel.Show(speedrun ? "sp_qual_comp" : "complete", true, new Action(() =>
            {
                FGTServiceManager.GetService<StatisticsService>().ProcessNewRound(StatisticsService.RoundResult.Qual);

                if (StateManager.IsPlayingExplore)
                {
                    StateManager.ExploreState.RequestNewRound();
                }
                else
                {
                    FGTLog(BepInEx.Logging.LogLevel.Error, "CreateMessageComplete", "Complete message was seen without an explore state.");
                    FLZ_Extensions.ForceExit();
                }
            }));

            return false;
        }

        [HarmonyPatch(typeof(BannersDefault), nameof(BannersDefault.CreateMessageQualified)), HarmonyPrefix]
        static bool CreateMessageQualified(BannersDefault __instance, Il2CppSystem.Action callback)
        {
            if (StateManager.IsPlayingExplore)
            {
                __instance.CreateMessageComplete();
                return false;
            }

            AudioManager.PlayGameplayEndAudio(true);
            __instance.State = BannersDefault.BannerActive.Qualified;
            __instance._isEliminateOrQualifiedMessageShowed = true;
            var speedrun = ConfigManager.SpeedrunMode.Value && !FGTServiceManager.GetService<SpeedrunService>().IsSepeedrunsDisabled;

            if (speedrun)
            {
                string txt = CMSLoader.Instance._localisedStrings._localisedStrings["qualified"];
                AddCMSString("sp_qual", txt[..^1] + ": " + FGTServiceManager.GetService<SpeedrunService>().ReturnTimerText());
                FGTServiceManager.GetService<SpeedrunService>().HandleState(RunState.Finish);
            }

            QualifiedScreenViewModel.Show(speedrun ? "sp_qual" : "qualified", new Action(() =>
            {
                FGTServiceManager.GetService<StatisticsService>().ProcessNewRound(StatisticsService.RoundResult.Qual);

                if (!LocalServerService.IsUserAloneAndHost)
                    return;

                if (StateManager.ShowState == null)
                {
                    if (speedrun && !CGM.IsTimerEnded())
                        FGTServiceManager.GetService<SpeedrunService>().TriggerSpeedrunContinueModal();
                    else
                    {
                        if (StateManager.ExploreState == null)
                            FLZ_Extensions.ForceExit(); //TEMP
                        else
                            StateManager.ExploreState.RequestNewRound();
                    }
                }
                else
                    StateManager.ShowState.OnShowProgress();
            }), __instance.GetTimeAttackEntry());

            GameActions.OnQualified?.Invoke();

            return false;
        }

        [HarmonyPatch(typeof(BannersDefault), nameof(BannersDefault.CreateMessageEliminated)), HarmonyPrefix]
        static bool CreateMessageEliminated(BannersDefault __instance)
        {
            AudioManager.PlayGameplayEndAudio(false);
            __instance.State = BannersDefault.BannerActive.Eliminated;
            __instance._isEliminateOrQualifiedMessageShowed = true;
            var speedrun = ConfigManager.SpeedrunMode.Value && !FGTServiceManager.GetService<SpeedrunService>().IsSepeedrunsDisabled;

            if (speedrun)
            {
                string txt = CMSLoader.Instance._localisedStrings._localisedStrings["eliminated"];
                AddCMSString("sp_elim", txt[..^1] + ": " + FGTServiceManager.GetService<SpeedrunService>().ReturnTimerText());

                if (!CGM.IsTimerEnded())
                    FGTServiceManager.GetService<SpeedrunService>().HandleState(RunState.Finish);
            }

            EliminatedScreenViewModel.Show(speedrun ? "sp_elim" : "eliminated", !LocalServerService.IsUserAloneAndHost ? new Action(__instance.SwitchToSpectator) : null, new Action(() =>
            {
                FGTServiceManager.GetService<StatisticsService>().ProcessNewRound(StatisticsService.RoundResult.Elim);

                if (!LocalServerService.IsUserAloneAndHost)
                {
                    __instance.ExitGame();
                    return;
                }

                if (StateManager.ShowState == null)
                {
                    if (speedrun && !CGM.IsTimerEnded())
                        FGTServiceManager.GetService<SpeedrunService>().TriggerSpeedrunRestart();
                    else
                    {
                        if (StateManager.ExploreState == null)
                            FLZ_Extensions.ForceExit(); //TEMP
                        else
                            StateManager.ExploreState.RequestNewRound();
                    }
                }
                else
                    StateManager.ShowState.OnShowProgress();

            }), __instance.GetTimeAttackEntry(), 5);
            
            GameActions.OnEliminated?.Invoke();
            
            return false;
        }

        [HarmonyPatch(typeof(BannersDefault), nameof(BannersDefault.HandleWonEpisode)), HarmonyPrefix]
        static bool CreateMessageWonEpisode(BannersDefault __instance)
        {
            AudioManager.PlayGameplayEndAudio(true);
            var speedrun = ConfigManager.SpeedrunMode.Value && !FGTServiceManager.GetService<SpeedrunService>().IsSepeedrunsDisabled;

            if (speedrun)
            {
                string txt = CMSLoader.Instance._localisedStrings._localisedStrings["winner"];
                AddCMSString("sp_win", txt[..^1] + ": " + FGTServiceManager.GetService<SpeedrunService>().ReturnTimerText());
                if (!CGM.IsTimerEnded())
                    FGTServiceManager.GetService<SpeedrunService>().HandleState(RunState.Finish);
            }

            WinnerScreenViewModel.Show(speedrun ? "sp_win" : "winner", true, new Action(() => 
            {
                if (!LocalServerService.IsUserAloneAndHost)
                {
                    __instance._clientGameManager.GotoResultState();
                    return;
                }

                if (StateManager.ShowState == null)
                {
                    if (speedrun)
                        FGTServiceManager.GetService<SpeedrunService>().TriggerSpeedrunRestart();
                    else
                    {
                        if (StateManager.ExploreState == null)
                            CGM.ShowResultsScreen();
                        else
                            StateManager.ExploreState.RequestNewRound();
                    }
                }
                else
                    StateManager.ShowState.OnShowProgress();
            }), __instance.GetTimeAttackEntry());

            GameActions.OnWon?.Invoke();

            return false;
        }
        

        //TEMP
        [HarmonyPatch(typeof(HexSnakeManager), nameof(HexSnakeManager.ManagePlayingParticipantCount)), HarmonyPrefix]
        static bool ManagePlayingParticipantCount(HexSnakeManager __instance, int playingParticipantCount)
        {
            playingParticipantCount = FGTServiceManager.GetService<RoundOptionsService>().GetPlayers();
            for (int i = 0; i < __instance.PlayerPlayAreaList.Count; i++)
            {
                __instance.PlayerPlayAreaList[i].SetActive(i < playingParticipantCount);
            }

            foreach (var test in Resources.FindObjectsOfTypeAll<COMMON_RoundProgressValueScaler>())
                test.enabled = false;

            return false;
        }

        [HarmonyPatch(typeof(MPGNetObjectManager), nameof(MPGNetObjectManager.SetupNetObjectOnGameObject)), HarmonyPostfix]
        static void SetupNetObjectOnGameObject(MPGNetObjectManager __instance, GameObject go, GameMessageServerSpawnObject msg)
        {
            GameActions.OnNetObjSpawned?.Invoke(msg.NetId, go, msg.NetObjectSpawnData.PrefabHash);
        }

        [HarmonyPatch(typeof(CGMDespatcher), nameof(CGMDespatcher.process), [typeof(GameMessageServerEventGeneric)]), HarmonyPostfix]
        static void OnServerEventGeneric(GameMessageServerEventGeneric msg)
        {
            switch (msg.Type)
            {
                case GameMessageServerEventGeneric.EventType.AllPlayersSpawned:
                    GameActions.OnAllPlayersSpawned?.Invoke();
                    break;
            }
        }

    }
}
