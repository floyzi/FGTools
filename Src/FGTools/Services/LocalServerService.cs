using Events;
using FG.Common;
using FG.Common.CMS;
using FGClient;
using FGTools.Config;
using FGTools.HarmonyPatches;
using FGTools.Internal;
using FGTools.LocalServer;
using FGTools.LocalServer.CustomMessages;
using FGTools.LocalServer.CustomMessages.Logic;
using FGTools.LocalServer.Implementations;
using FGTools.LocalServer.Patches;
using FGTools.Services.Logic;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Injection;
using Mediatonic.Networking;
using Mediatonic.Tools.Utils;
using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Serialization;
using UnityEngine;
using static FG.Common.FG_NetworkManager;
using static FGTools.Internal.Extensions.FLZ_Extensions;
using static FGTools.LocalServer.CustomMessages.Logic.CustomMessageManager;
using static FGTools.Services.LocalizationService;
using static FGTools.UI.ReadyPopups;

namespace FGTools.Services
{
    internal class LocalServerService : FGTService
    {
        internal static ServerManager ServerManager;
        internal static CustomMessageManager CustomMessageManager;
        internal static bool IsServerInOperation => ServerManager != null && NetworkServer.instance != null;
        internal static bool IsUserAloneAndHost => IsServerInOperation && ServerManager.GetConnections().Length == 1;
        internal static IGameStateView GameStateView;
        public override void RegisterService()
        {
            Broadcaster.Instance.Register<OnMainMenuDisplayed>(new Action<OnMainMenuDisplayed>(OnEnterMenu));
            Broadcaster.Instance.Register<OnDisplayLobby>(new Action<OnDisplayLobby>(OnDisplayLobby));
        }

#if LAN_MULTIPLAYER
        internal static void Join(string ip, int port)
        {
            try
            {
                var gsm = GlobalGameStateClient.Instance._gameStateMachine;
                var mmManager = GlobalGameStateClient.Instance._mainMenuManager;

                if (IsServerInOperation)
                    FGTServiceManager.Instance.GetService<LocalServerService>().ShutdownSerer(null);

                if (gsm.IsInState<StateMainMenu>() && mmManager != null)
                    gsm.CurrentState.Cast<StateMainMenu>().StartConnecting(ip, port, MatchmakingEnvironment.Production);

                Broadcaster.Instance.Broadcast(new OnDisplayLobby());
                SubscribeToEvents();
                //var state = new StateConnectToGame(GlobalGameStateClient.Instance._gameStateMachine, ip, port, "idk", false, GlobalGameStateClient.Instance.CreateClientGameStateData(), 1, false);
                //GlobalGameStateClient.Instance._gameStateMachine.ReplaceCurrentState(state.Cast<GameStateMachine.IGameState>());
            }
            catch (Exception ex)
            {
                ErrorPopup(ex, new Action<bool>(wasok =>
                {

                }), title: "server_generic_error_title", desc: "server_join_error", displayOnlyError: false, forceLeaveToMenu: true);
            }
        }
#endif

        internal void SingleplayerGame(Round round) => Host("127.0.0.1", 0, 1, round);

        internal static void Host(string ip, int port, int usedFor, Round TEMP_round)
        {
            if (usedFor == 0)
                throw new ArgumentException("Can't host the server for 0 players");

            try
            {
                var gsm = GlobalGameStateClient.Instance._gameStateMachine;
                var mmManager = GlobalGameStateClient.Instance._mainMenuManager;

                if (port >= 0)
                {
                    var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                    socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
                    port = ((IPEndPoint)socket.LocalEndPoint).Port;
                    socket.Close();
                }

                if (gsm.IsInState<StateMainMenu>() && mmManager != null)
                    gsm.CurrentState.Cast<StateMainMenu>().StartConnecting(ip, port, MatchmakingEnvironment.Production);

                if (IsServerInOperation)
                    FGTServiceManager.Instance.GetService<LocalServerService>().ShutdownSerer(null);

                if (!ClassInjector.IsTypeRegisteredInIl2Cpp<ServerReader>())
                    ClassInjector.RegisterTypeInIl2Cpp<ServerReader>();

                if (!ClassInjector.IsTypeRegisteredInIl2Cpp<ServerGameActions>())
                    ClassInjector.RegisterTypeInIl2Cpp<ServerGameActions>();

                if (!ClassInjector.IsTypeRegisteredInIl2Cpp<ServerGameStateView>())
                    ClassInjector.RegisterTypeInIl2Cpp<ServerGameStateView>();

                Plugin.ServerHarmony.PatchAll(typeof(ServerGameplayPatches));

                SubscribeToEvents();

                ServerReader _messageProcessor = new();
                NetworkRequest _networkRequest = FG_UnityInternetNetworkManager.HostServer(ip, port, _messageProcessor.Cast<INetworkMessageProcessor>());

                var actions = new ServerGameActions();
                ServerGameStateActions.Instance = new IGameStateServerActions(actions.Pointer);
                GameStateView = new ServerGameStateView().Cast<IGameStateView>();
                ServerManager = new(_networkRequest, _networkRequest.NetworkManager, TEMP_round, usedFor);

                if (!gsm.IsInState<StateConnectToGame>())
                {
                    var state = new StateConnectToGame(GlobalGameStateClient.Instance._gameStateMachine, ip, port, "local_entry_point", false, GlobalGameStateClient.Instance.CreateClientGameStateData(), 1, true);
                    GlobalGameStateClient.Instance._gameStateMachine.ReplaceCurrentState(state.Cast<GameStateMachine.IGameState>());
                }

                FGTLog(BepInEx.Logging.LogLevel.Info, typeof(LocalServerService), $"Hosting server on {ip}:{port}");
            }
            catch (Exception ex)
            {
                ErrorPopup(ex, new Action<bool>(wasok =>
                {

                }), title: "server_generic_error_title", desc: "server_host_error", displayOnlyError: false, forceLeaveToMenu: true);
            }
        }


        internal void ShutdownSerer(Action onShutdown)
        {
            if (!IsServerInOperation)
            {
                FGTLog(BepInEx.Logging.LogLevel.Warning, GetType(), $"Attempt to shutdown inactive server!");
                return;
            }

            FGTLog(BepInEx.Logging.LogLevel.Info, GetType(), $"Server commencing shutdown...");

            foreach (var obj in Enum.GetValues(typeof(FLZ_CustomMessage)))
            {
                NetworkServer.UnregisterHandler((byte)(FLZ_CustomMessage)obj);
            }

            NetworkServer.Shutdown();
            GlobalGameStateClient.Instance.ShutdownNetworkManager();

            UnSubscribeFromEvents();
            ServerManager.Shutdown();
            ServerManager = null;

            onShutdown?.Invoke();
        }

        static void OnClientReceiveMessage(EnumGameMessageType msgt)
        {
            switch (msgt)
            {
                case EnumGameMessageType.GMT_CLIENT_CONNECT_CLIENT:
                    break;
            }
        }

        static void SubscribeToEvents()
        {
            CustomMessageManager = new();
            Plugin.ServerHarmony.PatchAll(typeof(CommonServerPatches));
            Commands.OnRecivedMessage += OnClientReceiveMessage;

            CustomMessageDespatcher.OnClientSendAuthRequest += SendAuthRequest;
            CustomMessageDespatcher.OnClientSendUserInfo += SendCommonUserInfo;
            CustomMessageDespatcher.OnClientOutdatedHost += HostVersionOutdated;
            CustomMessageDespatcher.OnClientOutdatedClient += ClientVersionOutdated;
            CustomMessageDespatcher.OnClientVersionDifference += VersionDifference;

            var cSets = FGTServiceManager.GetService<ControllersDataService>();
            cSets.SetDataPreset(ConfigManager.OldPhysics.Value ? "10_8" : "Default");
        }

        static void UnSubscribeFromEvents()
        {
            CustomMessageManager = null;
            Plugin.ServerHarmony.UnpatchSelf();
            Commands.OnRecivedMessage -= OnClientReceiveMessage;

            CustomMessageDespatcher.OnClientSendAuthRequest -= SendAuthRequest;
            CustomMessageDespatcher.OnClientSendUserInfo -= SendCommonUserInfo;
            CustomMessageDespatcher.OnClientOutdatedHost -= HostVersionOutdated;
            CustomMessageDespatcher.OnClientOutdatedClient -= ClientVersionOutdated;
            CustomMessageDespatcher.OnClientVersionDifference -= VersionDifference;
        }

        void OnDisplayLobby(OnDisplayLobby e)
        {
            AudioManager.PlayOneShot(AudioManager.EventMasterData.LobbyFall);

            foreach (var shit in Resources.FindObjectsOfTypeAll<AFKManager>())
                GameObject.Destroy(shit);
        }

        void OnEnterMenu(OnMainMenuDisplayed e)
        {
            UnSubscribeFromEvents();
        }
        static void HostVersionOutdated()
        {
            DoModal("server_host_update_title", "server_host_update_desc", FGClient.UI.UIModalMessage.ModalType.MT_OK, FGClient.UI.UIModalMessage.OKButtonType.Default, act: new Action<bool>(wasok =>
            {

            })); GlobalGameStateClient.Instance._gameStateMachine.ReplaceCurrentState(new StateReloadingToMainMenu(GlobalGameStateClient.Instance._gameStateMachine, GlobalGameStateClient.Instance.CreateClientGameStateData()).Cast<GameStateMachine.IGameState>());
        }

        static void ClientVersionOutdated()
        {
            DoModal("server_join_update_title", "server_join_update_desc", FGClient.UI.UIModalMessage.ModalType.MT_OK, FGClient.UI.UIModalMessage.OKButtonType.Default, act: new Action<bool>(wasok =>
            {
                GlobalGameStateClient.Instance._gameStateMachine.ReplaceCurrentState(new StateReloadingToMainMenu(GlobalGameStateClient.Instance._gameStateMachine, GlobalGameStateClient.Instance.CreateClientGameStateData()).Cast<GameStateMachine.IGameState>());
            }));
        }

        static void VersionDifference()
        {
            DoModal("server_join_version_difference_title", "server_join_version_difference_desc", FGClient.UI.UIModalMessage.ModalType.MT_OK, FGClient.UI.UIModalMessage.OKButtonType.Default, act: new Action<bool>(wasok =>
            {
                GlobalGameStateClient.Instance._gameStateMachine.ReplaceCurrentState(new StateReloadingToMainMenu(GlobalGameStateClient.Instance._gameStateMachine, GlobalGameStateClient.Instance.CreateClientGameStateData()).Cast<GameStateMachine.IGameState>());
            }));
        }

        static void SendAuthRequest() => CustomMessageManager.SendMessageToServer(new GMC_ClientConnectRequest());
        static void SendCommonUserInfo() => CustomMessageManager.SendMessageToServer(new GMC_ClientUserInfo());

        public override void UpdateService()
        {
        }

        public override void DrawGUI()
        {
        }
    }
}
