using FG.Common;
using FG.Common.CMS;
using FGClient;
using FGClient.UI;
using FGTools.Config;
using FGTools.Internal;
using FGTools.LocalServer.CustomMessages.Logic;
using FGTools.LocalServer.Implementations;
using FGTools.Services;
using FGTools.States.Logic;
using HarmonyLib;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Levels.Progression;
using Mediatonic.Networking;
using Rewired;
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
    internal class CommonServerPatches : FGTBase
    {
        [HarmonyPatch(typeof(GlobalGameStateClient), nameof(GlobalGameStateClient.InitializeConnectionToServer)), HarmonyPostfix]
        static void OnClientConnected(GlobalGameStateClient __instance, bool wantsToSpectate, GameMessageBasePublicICopyable1ObfInUIInObFGInStBoInByUnique.EnumNPublicSealedva4vUnique clientType, ClientGameStateData gameStateData, int numLocalPlayers, string entryToken)
        {
            Commands.OnConnectedToServer?.Invoke();
        }

        [HarmonyPatch(typeof(ClientNetworkMessageProcessor), nameof(ClientNetworkMessageProcessor.processMessage)), HarmonyPrefix]
        static bool processMessage(ClientNetworkMessageProcessor __instance, GameConnection sender, GameMessageBase msg)
        {
            Commands.OnRecivedMessage?.Invoke(msg.getGameMessageType());
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

        [HarmonyPatch(typeof(NetworkConnection), nameof(NetworkConnection.SetHandlers)), HarmonyPrefix]
        static bool SetHandlers(NetworkConnection __instance, NetworkMessageHandlers handlers)
        {
            foreach (var obj in Enum.GetValues(typeof(FLZ_CustomMessage)))
            {
                FGTLog(BepInEx.Logging.LogLevel.Debug, "CONN - " + __instance.connectionId, $"Reserving handle [{(byte)(FLZ_CustomMessage)obj}] for custom messages");
                handlers.RegisterHandler((byte)(FLZ_CustomMessage)obj, DelegateSupport.ConvertDelegate<NetworkMessageDelegate>(LocalServerService.CustomMessageManager.OnCustomMessageReceived));
            }

            return true;
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
            Commands.OnRoundLoaded?.Invoke();
        }

        [HarmonyPatch(typeof(CheckpointManager), nameof(CheckpointManager.HandleCheckpointReached)), HarmonyPostfix]
        static void HandleCheckpointReached(CheckpointManager __instance, CheckpointZone cpz, MPGNetObject mpgno, ref bool __result)
        {
            if (__result)
                Commands.OnCheckpointReached?.Invoke(mpgno, cpz);
        }

        [HarmonyPatch(typeof(ClientGameManager), nameof(ClientGameManager.HandleLocalPlayerLapComplete)), HarmonyPostfix]
        static void HandleLocalPlayerLapComplete(ClientGameManager __instance, LocalPlayerLapCompleteEvent evt)
        {
            Commands.OnLapComplete?.Invoke();
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

        [HarmonyPatch(typeof(BannersDefault), nameof(BannersDefault.CreateMessageQualified)), HarmonyPrefix]
        static bool CreateMessageQualified(BannersDefault __instance, Il2CppSystem.Action callback)
        {
            AudioManager.PlayGameplayEndAudio(true);
            __instance.State = BannersDefault.BannerActive.Qualified;
            __instance._isEliminateOrQualifiedMessageShowed = true;

            if (ConfigManager.SpeedrunMode.Value)
            {
                string txt = CMSLoader.Instance._localisedStrings._localisedStrings["qualified"];
                AddCMSString("sp_qual", txt[..^1] + ": " + FGTServiceManager.GetService<SpeedrunService>().ReturnTimerText());
                FGTServiceManager.GetService<SpeedrunService>().HandleState(RunState.Finish);
            }

            QualifiedScreenViewModel.Show(ConfigManager.SpeedrunMode.Value ? "sp_qual" : "qualified", new Action(() =>
            {
                FGTServiceManager.GetService<StatisticsService>().ProcessNewRound(StatisticsService.RoundResult.Qual);

                if (!LocalServerService.IsUserAloneAndHost)
                    return;

                if (StateManager.ShowState == null)
                {
                    if (ConfigManager.SpeedrunMode.Value && !FGTServiceManager.GetService<SpeedrunService>().IsSepeedrunsDisabled)
                        FGTServiceManager.GetService<SpeedrunService>().TriggerSpeedrunContinueModal();
                }
                else
                    StateManager.ShowState.OnShowProgress();
            }), __instance.GetTimeAttackEntry());
            
            Commands.OnQualified?.Invoke();

            return false;
        }

        [HarmonyPatch(typeof(BannersDefault), nameof(BannersDefault.CreateMessageEliminated)), HarmonyPrefix]
        static bool CreateMessageEliminated(BannersDefault __instance)
        {
            AudioManager.PlayGameplayEndAudio(false);
            __instance.State = BannersDefault.BannerActive.Eliminated;
            __instance._isEliminateOrQualifiedMessageShowed = true;

            if (ConfigManager.SpeedrunMode.Value)
            {
                string txt = CMSLoader.Instance._localisedStrings._localisedStrings["eliminated"];
                AddCMSString("sp_elim", txt[..^1] + ": " + FGTServiceManager.GetService<SpeedrunService>().ReturnTimerText());
                FGTServiceManager.GetService<SpeedrunService>().HandleState(RunState.Finish);
            }

            EliminatedScreenViewModel.Show(ConfigManager.SpeedrunMode.Value ? "sp_elim" : "eliminated", !LocalServerService.IsUserAloneAndHost ? new Action(__instance.SwitchToSpectator) : null, new Action(() =>
            {
                FGTServiceManager.GetService<StatisticsService>().ProcessNewRound(StatisticsService.RoundResult.Elim);

                if (!LocalServerService.IsUserAloneAndHost)
                {
                    __instance.ExitGame();
                    return;
                }

                if (StateManager.ShowState == null)
                {
                    if (ConfigManager.SpeedrunMode.Value && !FGTServiceManager.GetService<SpeedrunService>().IsSepeedrunsDisabled)
                        FGTServiceManager.GetService<SpeedrunService>().TriggerSpeedrunRestart();
                }
                else
                    StateManager.ShowState.OnShowProgress();

            }), __instance.GetTimeAttackEntry(), 5);
            
            Commands.OnEliminated?.Invoke();
            
            return false;
        }

        [HarmonyPatch(typeof(BannersDefault), nameof(BannersDefault.CreateMessageWonEpisode)), HarmonyPrefix]
        static bool CreateMessageWonEpisode(BannersDefault __instance)
        {
            AudioManager.PlayGameplayEndAudio(true);

            if (ConfigManager.SpeedrunMode.Value)
            {
                string txt = CMSLoader.Instance._localisedStrings._localisedStrings["winner"];
                AddCMSString("sp_win", txt[..^1] + ": " + FGTServiceManager.GetService<SpeedrunService>().ReturnTimerText());
                FGTServiceManager.GetService<SpeedrunService>().HandleState(RunState.Finish);
            }

            WinnerScreenViewModel.Show(ConfigManager.SpeedrunMode.Value ? "sp_win" : "winner", true, new Action(() => 
            {
                if (!LocalServerService.IsUserAloneAndHost)
                {
                    __instance._clientGameManager.GotoResultState();
                    return;
                }

                if (StateManager.ShowState == null)
                {
                    if (ConfigManager.SpeedrunMode.Value && !FGTServiceManager.GetService<SpeedrunService>().IsSepeedrunsDisabled)
                        FGTServiceManager.GetService<SpeedrunService>().TriggerSpeedrunContinueModal();
                }
                else
                    StateManager.ShowState.OnShowProgress();
            }), __instance.GetTimeAttackEntry());

            Commands.OnWon?.Invoke();

            return false;
        }
    }
}
