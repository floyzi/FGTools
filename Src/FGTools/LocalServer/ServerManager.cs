using BepInEx.Logging;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using DG.Tweening.Plugins.Options;
using Events;
using FG.Common;
using FG.Common.Character;
using FG.Common.Character.MotorSystem;
using FG.Common.CMS;
using FG.Common.Fraggle;
using FG.Common.LODs;
using FG.Common.Messages;
using FG.Common.Network;
using FGClient;
using FGTools.Internal;
using FGTools.Internal.Behaviours;
using FGTools.LocalServer.CustomMessages;
using FGTools.LocalServer.CustomMessages.Logic;
using FGTools.Services;
using FGTools.States.Logic;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppSystem.Collections.Specialized;
using Levels.Obstacles;
using Levels.Progression;
using Levels.Rollout;
using Levels.TimeAttack;
using Mediatonic.Networking;
using SRF;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using UnityEngine;
using UniverseLib;
using static FG.Common.COMMON_ObjectiveBase;
using static FG.Common.FG_NetworkManager;
using static FGTools.Internal.Extensions.FLZ_Extensions;
using static RootMotion.FinalIK.IKSolverVR;

namespace FGTools.LocalServer
{
    internal class ServerManager : FGTBase
    {
        const int PlayerHash = -491682846;
        const int MaxAuthAttempts = 3;

        internal class NetworkedPlayer
        {
            public string Name;
            public MPGNetID NetId;
            public GameConnection Connection;
            public CustomisationSelections Cosmetics;
            public string Platform;
            public string AccountId;
            public int CrownRank;
            public int TeamID;
        }

        internal enum ServerState
        {
            Disconnected,
            Open,
            FillingGame,
            GameLoading,
            GameInProgress,
            Closing
        }


        readonly NetworkRequest Server;
        internal readonly FG_NetworkManager NetworkManager;
        readonly int LobbySize;
        readonly object GameInfo;
        internal FG_NetworkID ServerNetID;
        string NextRound;
        int NextSeed;
        ServerState State = ServerState.Disconnected;

        readonly Queue<GameMessageServerSpawnObject> PlayerSpawnQueue = [];
        readonly Dictionary<FG_NetworkID, NetworkedPlayer> ConnectedPlayers = [];
        readonly Dictionary<FG_NetworkID, GameConnection> PendingConnections = [];

        int SpawnedPlayers = 0, ReadyPlayers = 0, LoadedPlayers = 0;

        readonly Action<MPGNetObject> OnSpawnNetObj;
        readonly Action OnRoundLoaded;

        internal static Action<MPGNetObject> TimeAttackStart;
        internal static Action<MPGNetObject> TimeAttackFinish;
        internal static Action<MPGNetObject, uint, FG_NetworkID, string, string, string, string, uint, int, string, int, bool, CustomisationSelections> OnPlayerSpawned;
        internal static Action<NetworkMessage> OnServerReceivedMessage;

        public ServerManager(NetworkRequest startTicket, FG_NetworkManager netManager, object gameInfo, int lobbySize = 1)
        {
            Server = startTicket;
            NetworkManager = netManager;
            LobbySize = lobbySize;
            ServerNetID = GlobalGameStateClient.Instance.GetLocalClientNetworkID();

            if (GameMessageFactory._messagePool == null)
                GameMessageFactory.Initialize(true);

            OnSpawnNetObj = new Action<MPGNetObject>(OnNetObjSpawned);
            GlobalGameStateClient.Instance.NetObjectManager.OnSpawnNetObject += OnSpawnNetObj;

            OnPlayerSpawned += OnPlayerSpawnedServer;
            OnRoundLoaded += PrepareForNetworkedGame;

            ServerDespatcher.OnPing += OnPing;
            ServerDespatcher.OnClientConnectInitial += AuthPlayer;
            ServerDespatcher.OnClientConnectClient += FG_ClientConnectRequest;
            ServerDespatcher.OnSetReady += OnSetReady;
            ServerDespatcher.OnSpawnPlayer += SpawnPlayer;
            ServerDespatcher.OnMotorTasks += OnMotorTasks;
            ServerDespatcher.OnDisconnectPlayer += OnDisconnect;
            ServerDespatcher.OnTimeAttackReset += OnTimeAttackReset;

            CustomMessageDespatcher.OnServerConnectRequest += CustomMessageDespatcher_ClientConnectRequest;
            CustomMessageDespatcher.OnServerUserInfo += CustomMessageDespatcher_OnClientUserInfo;

            Commands.OnCheckpointReached += OnCheckpointReached;

            COMMON_ObjectiveBase.m_OnObjectiveSatisfied_SERVERONLY = DelegateSupport.ConvertDelegate<HandleObjectiveSatisfied>(ObjectiveAchived);

            TimeAttackStart += OnTimeAttackStart;
            TimeAttackFinish += OnTimeAttackEnd;

            GameInfo = gameInfo;

            foreach (var fgcc in Resources.FindObjectsOfTypeAll<FallGuysCharacterController>())
                fgcc.MotorAgent._motorFunctionsConfig = MotorAgent.MotorAgentConfiguration.Offline;

            Commands.OnRoundLoaded += OnRoundLoaded;

            HandleServerState(ServerState.Open);
        }

        private void CustomMessageDespatcher_OnClientUserInfo(GMC_ClientUserInfo msg)
        {
            if (!PendingConnections.TryGetValue(msg.NetworkID, out var pendingConn))
            {
                FGTLog(LogLevel.Error, GetType(), $"Unable to find pending connection out Network ID \"{pendingConn.RemoteNetworkID}\"");
                return;
            }

            var conn = NetworkManager.GetConnectionForNetworkID(msg.NetworkID);

            if (conn == null)
            {
                FGTLog(LogLevel.Error, GetType(), $"Unable to find connection out Network ID \"{pendingConn.RemoteNetworkID}\"");
                return;
            }

            if (ConnectedPlayers.ContainsKey(msg.NetworkID))
            {
                FGTLog(LogLevel.Warning, GetType(), $"Network ID \"{pendingConn.RemoteNetworkID}\" already passed authentication");
                return;
            }

            ConnectedPlayers.Add(msg.NetworkID, new()
            {
                AccountId = msg.AccountID,
                Connection = conn,
                Cosmetics = msg.CreateSelections(),
                CrownRank = msg.CrownRank,
                Name = msg.Username,
                NetId = null,
                TeamID = -1,
                Platform = msg.Platform,
            });

            NetworkManager.SendMessageToClient(conn, new GameMessageServerConnectedClient()
            {
                ServerSessionId = "flz_local_session",
                ClientRemoteAddress = "127.0.0.1",
                EpisodeGuid = new Il2CppSystem.Guid(Guid.NewGuid().ToString()),
                IsCrossPlatform = new Il2CppSystem.Nullable<bool>(true),
                PlayerIds = new Il2CppStructArray<uint>([1]),
                ResponseCode = GameMessageServerConnectedClient.EnumConnectionResponse.ECT_SUCCESSFUL_PARTICIPANT,
                ServerBuildInfo = "local_fgt_server",
                ServerId = 102,
                ServerTime = Il2CppSystem.DateTime.UtcNow,
                ShowId = "classic_solo_main_show",
            }, DeliveryType.Reliable);

            PendingConnections.Remove(pendingConn.RemoteNetworkID);

            if (State == ServerState.FillingGame)
                BroadcastOptions();

            if (ConnectedPlayers.Count == LobbySize)
                StartLoadingRound();
        }

        void OnCheckpointReached(MPGNetObject evt, CheckpointZone zone)
        {
            if (!CGM.GameRules.IsTimeAttackGameMode) return;

            var manager = Resources.FindObjectsOfTypeAll<TimeAttackManager>().FirstOrDefault();
            if (manager == null)
                return;

            var playerData = manager.GetPlayerStats(evt.NetID.m_NetworkID);
            if (playerData.GetCurrentLap.LapState != TimeAttackLapState.InProgress)
                return;

            playerData.GetCurrentLap.ElapsedTime.Add(playerData.GetCurrentLap.CurrentLapTime);

            BroadcastMessage(new GameMessageServerTimeAttackRegistered()
            {
                RemoteId = evt.NetID.m_NetworkID,
                LapState = TimeAttackLapState.InProgress,
                LapCount = (ushort)playerData._lapData.Count,
                MessageType = TimeAttackUpdateType.Split,
                Deductions = new(playerData.GetCurrentLap.Deductions.ToArray()),
                ElapsedTimes = new(playerData.GetCurrentLap.ElapsedTime.ToArray()),
                PauseTime = 0.0f
            });
        }

        void OnTimeAttackStart(MPGNetObject evt)
        {
            Debug.Assert(!CGM.GameRules.IsTimeAttackGameMode, $"OnTimeAttackStart being called on non time attack game mode");

            var manager = Resources.FindObjectsOfTypeAll<TimeAttackManager>().FirstOrDefault();
            if (manager == null)
                return;

            var playerData = manager.GetPlayerStats(evt.NetID.m_NetworkID);
            if (playerData.GetCurrentLap.LapState != TimeAttackLapState.NotStarted)
                return;

            BroadcastMessage(new GameMessageServerTimeAttackRegistered()
            {
                RemoteId = evt.NetID.m_NetworkID,
                LapState = TimeAttackLapState.InProgress,
                LapCount = (ushort)playerData._lapData.Count,
                MessageType = TimeAttackUpdateType.Split,
                Deductions = new(playerData.GetCurrentLap.Deductions.ToArray()),
                ElapsedTimes = new(playerData.GetCurrentLap.ElapsedTime.ToArray()),
                PauseTime = 0.0f
            });
        }

        void OnTimeAttackEnd(MPGNetObject evt)
        {
            Debug.Assert(!CGM.GameRules.IsTimeAttackGameMode, $"OnTimeAttackEnd being called on non time attack game mode");

            var manager = Resources.FindObjectsOfTypeAll<TimeAttackManager>().FirstOrDefault();
            if (manager == null)
                return;

            var playerData = manager.GetPlayerStats(evt.NetID.m_NetworkID);
            if (playerData.GetCurrentLap.LapState != TimeAttackLapState.InProgress)
                return;

            BroadcastMessage(new GameMessageServerTimeAttackRegistered()
            {
                RemoteId = evt.NetID.m_NetworkID,
                LapState = TimeAttackLapState.Finished,
                LapCount = (ushort)playerData._lapData.Count,
                MessageType = TimeAttackUpdateType.Split,
                Deductions = new(playerData.GetCurrentLap.Deductions.ToArray()),
                ElapsedTimes = new(playerData.GetCurrentLap.ElapsedTime.ToArray()),
                PauseTime = 0.0f
            });
           
        }

        void PrepareForNetworkedGame()
        {
            var rolloutManager = Resources.FindObjectsOfTypeAll<RolloutManager>().FirstOrDefault();

            if (rolloutManager != null && rolloutManager.gameObject.activeSelf)
            {
                var net = rolloutManager.gameObject.AddComponent<MPGNetObject>();
                net.NetID = GlobalGameStateClient.Instance.NetObjectManager.GetNextNetID();
                net.UniqueId = rolloutManager.GetComponent<MPGNetObjectPossessable>().UniqueId;
                net._gameObjectHash = net.GenerateGameObjectHash(NetObjectCreationMode.Possess);

                var RES = new Il2CppSystem.Collections.Generic.List<int>();
                int ringSchemas = rolloutManager._ringSegmentSchemas.Count;
                int ringLimit = UnityEngine.Random.Range(2, ringSchemas);
                int addedRings = 0;

                for (int i = 0; i < ringSchemas; i++)
                {
                    if (addedRings < ringSchemas)
                    {
                        int ringMax = UnityEngine.Random.Range(0, rolloutManager._ringSegmentSchemas[i].PrefabPool.Count);
                        RES.Add(ringMax);
                        addedRings++;
                    }
                }

                rolloutManager.SetSelectedPrefabIndexes(RES);

                var spawnData = new GameObjectSpawnData();
                spawnData.FromGameObject(rolloutManager.gameObject, false);
                BroadcastMessage(new GameMessageServerSpawnObject()
                {
                    _netObjectSpawnData = new()
                    {
                        Position = rolloutManager.transform.position,
                        Rotation = rolloutManager.transform.rotation,
                        _additionalSpawnData = spawnData,
                        _spawnObjectType = EnumSpawnObjectType.OBJECT,
                        _creationMode = NetObjectCreationMode.Possess,
                        _prefabHash = net.GameObjectHash,
                        _useUnifiedSetup = false,

                    },

                });
            }

            foreach (PlayerRatioedBulkItemSpawner spawner in Resources.FindObjectsOfTypeAll<PlayerRatioedBulkItemSpawner>())
            {
                int itemCount = Mathf.Clamp(Mathf.RoundToInt(LobbySize * spawner._numberOfItemsPerPlayer), spawner._minItems, spawner._maxItems);

                Dictionary<GameObject, List<Transform>> spawns = [];

                if (spawner.ItemParents.Count > 0)
                {
                    foreach (Transform trans in spawner.ItemParents)
                    {
                        var spawn = new List<Transform>();
                        for (int i = 0; i < trans.childCount; i++)
                            spawn.Add(trans.GetChild(i));

                        spawns.Add(trans.gameObject, spawn);
                    }
                }
                else if (spawner.ItemParent != null)
                {
                    for (int i = 0; i < spawner.ItemParent.childCount; i++)
                    {
                        var spawn = new List<Transform>();

                        var child = spawner.ItemParent.GetChild(i);
                        for (int j = 0; j < child.childCount; j++)
                            spawn.Add(child.GetChild(j));

                        spawns.Add(child.gameObject, spawn);
                    }
                }

                itemCount = Mathf.Min(itemCount, spawns.Sum(pair => pair.Value.Count));
                itemCount = itemCount / spawns.Count * spawns.Count;

                foreach (var pair in spawns)
                {
                    for (int j = 0; j < itemCount / spawns.Count && j < pair.Value.Count; j++)
                    {
                        var targetObject = spawner.ItemPrefab.GetComponent<NetworkAwareGeneric>().SpawnObject;

                        if (!targetObject.TryGetComponent<MPGNetObject>(out var netObj))
                            netObj = targetObject.gameObject.AddComponent<MPGNetObject>();

                        targetObject.RemoveComponentIfExists<NetworkAwareGeneric>();
                        netObj.NetID = GlobalGameStateClient.Instance.NetObjectManager.GetNextNetID();
                        netObj.GameObjectHash = netObj.GenerateGameObjectHash(NetObjectCreationMode.Spawn);

                        var spawn = pair.Value[j];
                        netObj.SpawnPrefab(spawn.position, spawn.rotation, spawn.localScale);
                    }
                }
            }

            GameObject ServerManager = new() { name = "ServerManager" };
            ServerManager.AddComponent<ServerBehaviour>();
        }

        void HandleServerState(ServerState state)
        {
            ServerLog("HandleServerState", $"Server state change: {State} --> {state}");
            State = state;

            switch (state)
            {
                case ServerState.Open:
                    break;
            }
        }

        void ObjectiveAchived(MPGNetID playerObjectNetID, COMMON_ObjectiveBase pObjective)
        {
            ServerGameStateActions.Instance.MarkPlayerAsSuccessful(CGM.GetNetObjectByID(playerObjectNetID), true);
        }

        void OnPing(GameMessagePing msg, GameConnection playerConn)
        {
            var pong = new GameMessagePong();
            pong.InitialiseFromPing(msg);
            NetworkManager.SendMessageToClient(playerConn, pong, DeliveryType.Reliable);
        }

        void AuthPlayer(GameMessageClientConnectInitial msg, GameConnection playerConn)
        {
            playerConn.AdvanceToConnectedState();

            if (State != ServerState.FillingGame)
            {
                HandleServerState(ServerState.FillingGame);

                if (NextSeed == 0)
                    NextSeed = FGTServiceManager.GetService<RoundOptionsService>().GetSeedForGame();

                switch (GameInfo)
                {
                    case string s:
                        NextRound = s;
                        break;
                    case Round r:
                        NextRound = r.Id;
                        break;
                }
            }

            NetworkManager.SendMessageToClient(playerConn, new GameMessageServerConnectInitial()
            {
                ServerKey = 102,
                _random = new(),
            }, DeliveryType.Reliable);
        }

        //void OnUserInfoReceived(GMC_UserInfo userInfo)
        //{
        //    if (State == ServerState.Closing)
        //        return;

        //    try
        //    {
        //        var netId = new FG_NetworkID(userInfo.NetworkID);
        //        if (!WaitingForAuth.ContainsKey(netId))
        //            WaitingForAuth.Add(netId, userInfo);
        //        else
        //            WaitingForAuth[netId] = userInfo;
        //    }
        //    catch (Exception e)
        //    {
        //        FGTLog(LogLevel.Error, GetType(), $"We received bad user info, {e}");
        //    }
        //}

        //IEnumerator AuthPlayerForGame(GameConnection conn, Action authSuccess, Action authFailure)
        //{
        //    int authAttempt = 0;

        //    while (authAttempt < MaxAuthAttempts)
        //    {
        //        FGTLog(LogLevel.Info, GetType(), $"Trying to auth player {conn.RemoteNetworkID}... attempt {++authAttempt}/{MaxAuthAttempts}");

        //        if (WaitingForAuth.TryGetValue(conn.RemoteNetworkID, out var authData))
        //        {
        //            ConnectedPlayers.Add(conn.RemoteNetworkID, new()
        //            {
        //                Connection = conn,
        //                NetId = null,
        //                Name = authData.Username,
        //                Cosmetics = authData.CreateSelections(),
        //                AccountId = authData.AccountID,
        //                Platform = authData.Platform,
        //                CrownRank = authData.CrownRank,
        //                TeamID = -1
        //            });

        //            WaitingForAuth.Remove(conn.RemoteNetworkID);

        //            conn.AdvanceToLoggedInState();

        //            FGTLog(LogLevel.Info, GetType(), $"Player {authData.Username} completed authentication!");
        //            authSuccess();
        //            yield break;
        //        }
        //        else
        //        {
        //            yield return new WaitForSeconds(1.5f);
        //        }
        //    }

        //    FGTLog(LogLevel.Warning, GetType(), $"Player {conn.RemoteNetworkID} failed authentication, we didn't received common info.");

        //    authFailure();

        //}

        void FG_ClientConnectRequest(GameMessageBasePublicICopyable1ObfInUIInObFGInStBoInByUnique msg, GameConnection playerConn)
        {
            if (State == ServerState.Closing)
                return;

            playerConn.RemoteNetworkID = msg.field_Public_FG_NetworkID_0;
            PendingConnections.Add(playerConn.RemoteNetworkID, playerConn);

            LocalServerService.CustomMessageManager.SendMessageToClient(new GMC_ServerConnectionStatus()
            {
                Status = GMC_ServerConnectionStatus.ServerResponse.AUTHENTICATION_REQUIRED
            }, playerConn);
        }

        void CustomMessageDespatcher_ClientConnectRequest(GMC_ClientConnectRequest msg)
        {
            if (!PendingConnections.TryGetValue(msg.NetworkID, out var pendingConn))
            {
                FGTLog(LogLevel.Error, GetType(), $"Unable to find pending connection out Network ID \"{pendingConn.RemoteNetworkID}\"");
                return;
            }

            FGTLog(LogLevel.Info, GetType(), $"Processing connect request from {msg.NetworkID}");

            var servVer = new System.Version(Plugin.BuildInfo.Version);
            var clientVer = new System.Version(msg.Version);
            var conn = NetworkManager.GetConnectionForNetworkID(msg.NetworkID);

            if (conn == null)
            {
                FGTLog(LogLevel.Error, GetType(), $"Unable to find connection out Network ID \"{pendingConn.RemoteNetworkID}\"");
                return;
            }

            if (servVer > clientVer)
            {
                FGTLog(LogLevel.Warning, GetType(), $"Server running on an newer version than client, disconnecting...");

                LocalServerService.CustomMessageManager.SendMessageToClient(new GMC_ServerConnectionStatus()
                {
                    Status = GMC_ServerConnectionStatus.ServerResponse.JOIN_VERSION_OUTDATED,
                }, conn);

                NetworkManager.SendMessageToClient(conn, new GameMessageServerConnectedClient()
                {
                    ResponseCode = GameMessageServerConnectedClient.EnumConnectionResponse.ECT_FAILED_CHECK_1
                }, DeliveryType.Reliable);

                PendingConnections.Remove(pendingConn.RemoteNetworkID);
                return;
            }

            if (clientVer > servVer)
            {
                FGTLog(LogLevel.Warning, GetType(), $"Client running on an newer version than server, disconnecting...");

                LocalServerService.CustomMessageManager.SendMessageToClient(new GMC_ServerConnectionStatus()
                {
                    Status = GMC_ServerConnectionStatus.ServerResponse.HOST_VERSION_OUTDATED,
                }, conn);

                NetworkManager.SendMessageToClient(conn, new GameMessageServerConnectedClient()
                {
                    ResponseCode = GameMessageServerConnectedClient.EnumConnectionResponse.ECT_FAILED_CHECK_1
                }, DeliveryType.Reliable);

                PendingConnections.Remove(pendingConn.RemoteNetworkID);
                return;
            }

            if (clientVer == servVer && msg.ID != Plugin.BuildInfo.GUID)
            {
                FGTLog(LogLevel.Warning, GetType(), $"Server and client running on a different version (build ID doesn't match), disconnecting...");

                LocalServerService.CustomMessageManager.SendMessageToClient(new GMC_ServerConnectionStatus()
                {
                    Status = GMC_ServerConnectionStatus.ServerResponse.VERSION_DIFFERENCE,
                }, conn);

                NetworkManager.SendMessageToClient(conn, new GameMessageServerConnectedClient()
                {
                    ResponseCode = GameMessageServerConnectedClient.EnumConnectionResponse.ECT_FAILED_CHECK_1
                }, DeliveryType.Reliable);

                PendingConnections.Remove(pendingConn.RemoteNetworkID);
                return;
            }

            FGTLog(LogLevel.Info, GetType(), $"{conn.RemoteNetworkID} completed authentication, waiting for user info");

            LocalServerService.CustomMessageManager.SendMessageToClient(new GMC_ServerConnectionStatus()
            {
                Status = GMC_ServerConnectionStatus.ServerResponse.PLAYER_INFO_REQUIRED,
            }, conn);
        }

        void BroadcastOptions()
        {
            if (State == ServerState.Closing)
                return;

            Round nextRound;

            if (GameInfo.GetType() == typeof(Round))
                nextRound = (Round)GameInfo;
            else
                nextRound = CMSLoader.Instance.CMSData.Rounds[NextRound];

            //nextRound.GameRules.StartGameMessage = new()
            //{
            //    Title = new() { Id = "test_01", Text = "TITLE" },
            //    Body = new() { Id = "test_02", Text = "BODY" }
            //};
            //nextRound.GameRules.StartGameMessageTrigger = StartGameMessageTrigger.Intro;
            //nextRound.GameRules.StartGameMessageFormat = UIOverlayMessageFormat.Popup;

            NetworkGameData.SetGameOptionsFromRoundData(nextRound);
            NetworkGameData.SetInitialRoundPlayerCount((uint)LobbySize);

            var newOptions = NetworkGameData.currentGameOptions_;

            newOptions._currentParticipantCount = NetworkManager.ConnectedClients;
            newOptions._maxParticipantsToStart = LobbySize;
            newOptions._directorSeed = NextSeed;
            newOptions._randomSeed = NextSeed;
            newOptions.RoundIndex = 1;

            NetworkGameData.currentGameOptions_ = newOptions;

            BroadcastMessage(new GameMessageServerGameDataOptions()
            {
                _gameOptions = NetworkGameData.currentGameOptions_,

            });
        }

        void StartLoadingRound()
        {
            if (State == ServerState.Closing || State == ServerState.GameInProgress || State == ServerState.GameLoading)
                return;

            HandleServerState(ServerState.GameLoading);
            BroadcastMessage(new GameMessageServerStartLoadingLevel()
            {
            });
        }

        private void OnSetReady(GameMessageClientSetReady msg, GameConnection playerConn)
        {
            if (State == ServerState.Closing)
                return;

            switch (msg.readinessState)
            {
                case PlayerReadinessState.ReceivedLevelDetails:
                    break;
                case PlayerReadinessState.LevelLoaded:
                    LoadedPlayers++;
                    TryToPerfomSpawns();
                    break;
                case PlayerReadinessState.ReadyToPlay:
                    ReadyPlayers++;
                    CheckIfWeReadyToPlay();
                    break;
            }
        }

        void TryToPerfomSpawns()
        {
            if (State == ServerState.Closing && State != ServerState.GameInProgress)
                return;

            if (LoadedPlayers == LobbySize && PlayerSpawnQueue.Count == NetworkManager.ConnectedClients)
            {
                CGM.GameRules.PreparePlayerStartingPositions(1);

                while (PlayerSpawnQueue.Count > 0)
                {
                    try
                    {
                        var spawnAct = PlayerSpawnQueue.Dequeue();
                        var pos = CGM.GameRules.PickStartingPosition(1, 0, -1, 0, false);

                        spawnAct._netObjectSpawnData._position = pos.transform.position;
                        spawnAct._netObjectSpawnData._rotation = pos.transform.rotation;

                        GlobalGameStateClient.Instance.NetObjectManager.SpawnNetObject(spawnAct);
                        BroadcastMessage(spawnAct, [ServerNetID]);

                        ServerLog("TryToPerfomSpawns", $"Broadcasted spawn for player {spawnAct.NetId}... Remain {PlayerSpawnQueue.Count}");
                    }
                    catch (Exception e)
                    {
                    }
                }
            }
        }

        void CheckIfWeReadyToPlay()
        {
            if (State == ServerState.Closing)
                return;

            if (ReadyPlayers == NetworkManager.ConnectedClients && State != ServerState.GameInProgress)
            {
                var teams = CGM.GameRules.NumTeamsWanted();
                var vsGroups = CGM.GameRules.NumVsGroupsWanted(ReadyPlayers);

                Il2CppSystem.Collections.Generic.List<int> initialScores = new(teams);
                Il2CppSystem.Collections.Generic.Dictionary<MPGNetID, int> teamAssigments = new(ReadyPlayers);
                Il2CppSystem.Collections.Generic.Dictionary<int, int> vsGroupAssigments = new(ReadyPlayers);

                if (teams > 0)
                {
                    for (int i = 0; i < teams; i++)
                    {
                        initialScores.Add(0);
                    }

                    for (int i = 0; i < CGM.AllPlayers.Count; i++)
                    {
                        var p = CGM._clientPlayerManager._players[i];

                        if (p.objectNetID == default)
                            continue;

                        teamAssigments.Add(p.objectNetID, 1);
                    }
                }

                if (vsGroups > 0)
                {
                    for (int i = 0; i < ReadyPlayers; i++)
                    {

                    }
                }

                HandleServerState(ServerState.GameInProgress);

                foreach (var player in Resources.FindObjectsOfTypeAll<FallGuysCharacterController>())
                {
                    if (player.NetObject != null && player.NetObject.IsValidNetObject())
                        player.gameObject.AddComponent<ServerControlledObject>();
                }

                RMIBehaviourManager.SetActive();

                if (CGM.IsUGCRound)
                {
                    FraggleCommonManager.Instance.IsInLevelEditor = true;
                    FraggleCommonManager.Instance.SetModeToExplore(new());
                }

                var startGame = new GameMessageServerStartGame()
                {
                    RoundGuid = Il2CppSystem.Guid.NewGuid(),
                    EndRoundTime = FGTServiceManager.GetService<RoundOptionsService>().GetRoundLength(CGM._round.GameRules),
                    EntityAssignments = new(),
                    InitialNumParticipants = LobbySize,
                    InitialTeamScores = initialScores,
                    NumBotsRemaining = 0,
                    NumEntities = NetworkManager.ConnectedClients,
                    NumTeams = teams,
                    NumVsGroups = vsGroups,
                    TeamAssignments = teamAssigments,
                    VsGroupAssignments = vsGroupAssigments,
                    StartRoundTime = 5,

                };

                BroadcastMessage(new GameMessageServerQualificationProgressUpdated()
                {
                    NumQualifiedPlayers = 0,
                    NumEliminatedPlayers = 0,
                });

                BroadcastMessage(startGame);

                ReadyPlayers = -1;
                SpawnedPlayers = -1;
                LoadedPlayers = -1;
            }
        }

        void SpawnPlayer(GameMessageClientRequestSpawnPlayer msg, GameConnection conn)
        {
            if (State == ServerState.Closing)
                return;

            uint pid = (uint)GlobalGameStateClient.Instance.NetObjectManager.GetNextNetID();

            var meta = ConnectedPlayers[msg._networkID];
            ServerLog("SpawnPlayer", $"Attempting to spawn player. Name={meta.Name} Id={msg._localPlayerIndex} NetId={msg._networkID} PlayerMPGId={pid}");
            PlayerSpawnQueue.Enqueue(CreatePlayerSpawnRequest(conn, pid, ref meta));

            TryToPerfomSpawns();
        }

        void OnTimeAttackReset(GameMessageClientTimeAttackReset msg, GameConnection conn)
        {
            var player = ConnectedPlayers[conn.RemoteNetworkID];
            var playerObject = CGM.GetNetObjectByID(player.NetId);
            var playerData = CGM.GetPlayerData(player.NetId);
            var spawn = CGM.GameRules.PickRespawnPosition((int)playerData.objectNetID.m_NetworkID, playerData.SquadID, playerData.TeamID, playerData.VsGroupID, false);

            var manager = Resources.FindObjectsOfTypeAll<TimeAttackManager>().FirstOrDefault();
            if (manager == null)
                return;

            var playerAttackData = manager.GetPlayerStats(playerData.objectNetID.m_NetworkID);

            if (playerAttackData.GetCurrentLap.LapState == TimeAttackLapState.InProgress || playerAttackData.GetCurrentLap.LapState == TimeAttackLapState.Finished)
            {
                playerAttackData.CompleteLap();
                playerAttackData.AddNewLap(TimeAttackUpdateType.Restart);

                var checkManag = Resources.FindObjectsOfTypeAll<CheckpointManager>().FirstOrDefault();
                if (checkManag == null)
                    return;

                checkManag.OnResetPlayer(playerData.objectNetID, true);

                BroadcastMessage(new GameMessageServerTimeAttackRegistered()
                {
                    RemoteId = playerObject.NetID.m_NetworkID,
                    LapState = TimeAttackLapState.NotStarted,
                    LapCount = (ushort)playerAttackData._lapData.Count,
                    MessageType = TimeAttackUpdateType.Restart,
                    Deductions = new([0.0f]),
                    ElapsedTimes = new([0.0f]),
                    PauseTime = 0.0f
                });
            }

            ServerGameStateActions.Instance.TeleportNetObject(playerObject, spawn.transform.position, spawn.transform.rotation, LiveOps.Challenges.SpawnReason.Respawn);
        }

        void OnDisconnect(GameMessageClientDisconnectPlayer msg, GameConnection conn)
        {
            if (State == ServerState.Disconnected)
                return;

            MPGNetObject obj = null;
            if (State == ServerState.GameInProgress || State == ServerState.GameLoading && GlobalGameStateClient.Instance.NetObjectManager.TryGetNetObject(ConnectedPlayers[msg.clientNetworkID].NetId, out obj))
               ServerGameStateActions.Instance.RequestDestroy(obj);

            ConnectedPlayers.Remove(msg.clientNetworkID);

            if (State == ServerState.FillingGame)
                BroadcastOptions();
        }

        bool CastTask<T>(MotorTask task, out T result) where T : MotorTask
        {
            if (task.GetActualType() == typeof(T))
            {
                result = task.Cast<T>();
                return true;
            }
            result = null;
            return false;
        }

        T GetMotorTask<T>(Il2CppReferenceArray<MotorTask> arr) where T : MotorTask
        {
            for (int i = 0; i < arr.Count; i++) {
                var task = arr[i];

                if (task.GetActualType() == typeof(T))
                    return task.Cast<T>();
            }

            return null;
        }

        internal void ParseTasks(MotorTask[] arr, FallGuysCharacterController fg)
        {
            //FGTLog(LogLevel.Info, GetType(), $"Reading tasks of {fg.name} ({arr.Length})");

            bool local = fg.IsLocalPlayer;

            for (int j = 0; j < arr.Length; j++)
            {
                var task = arr[j];
                //FGTLog(LogLevel.Info, GetType(), $"{task.GetActualType()} - {j}");

                if (CastTask(task, out MotorTaskActivatePowerup pow))
                {
                    //FGTLog(LogLevel.Info, GetType(), $"{pow.ToString()}");
                    var a = fg.PowerupMotorFunction.Cast<OfflineMotorFunctionPowerup>();
                    if (pow.isRequested)
                        a._currentState.Cast<OfflineMotorFunctionPowerupStateUse>().OnServerConfirmedState();
                }

                if (local)
                    continue;

                if (CastTask(task, out MotorTaskMove move))
                {
                    fg.MovementMotorFunction.ApplyNormalMovement(move.DesiredMove, fg.MovementMotorFunction.MaxSpeed, MotorFunctionMovement.UpdateVelocityMode.IgnoreAngle);
                }

                if (CastTask(task, out MotorTaskEmote emote))
                {
                    fg.MotorAgent.GetMotorFunction<MotorFunctionEmote>().emoteVariation = emote.emoteVariation;
                }

                if (CastTask(task, out MotorTaskRagdoll ragdoll))
                {
                    if (ragdoll.DesiredState == MotorTaskRagdoll.State.Pinned && fg.RagdollMotorFunction.CurrentID != fg.RagdollMotorFunction.InactiveStateID)
                        fg.RagdollMotorFunction.SetState(fg.RagdollMotorFunction.InactiveStateID);
                }

                if (CastTask(task, out MotorTaskSpeech speech))
                {
                    var speechFunc = fg.SpeechMotorFunction;
                    speechFunc._requestEndTimestamps.Clear();

                    if (speech.isRequested && speechFunc._spammingLockTimeEnd < CGM.CurrentGameSession.SimulationTime && speechFunc.SpeechUseTimestamp != speech.useTime)
                        speechFunc.SetCurrentSpeechOption(speech.speechIndex, speech.useTime);
                }

                if (CastTask(task, out MotorTaskPortal portal))
                {
                    var portalFunc = fg.PortalMotorFunction;

                    if (portal.isRequested)
                    {
                        //portalFunc._lastPositionBeforePortal = fg.transform.position;

                        //portalFunc.AcknowledgeTeleportAttempt_Server(true, portal.PortalData);
                        //portalFunc.VerifyActivePortalTask_Server();

                        //portalFunc.SetState(portalFunc.GetState<MotorFunctionPortalStateActive>().ID);
                    }
                }

                if (CastTask(task, out MotorTaskPiggyback piggi))
                {
                    var pl = fg.PiggybackMotorFunction;

                    if (piggi.isRequested)
                    {
                        if (piggi.PiggybackTarget.Status == PiggybackTargetStatus.AskingForHelp && !pl.IsAskingForHelp)
                        {
                            pl.SetState(MotorFunctionPiggyback.askingForHelpStateID);
                        }

                        if (pl.IsAskingForHelp && piggi.PiggybackTarget.Status == PiggybackTargetStatus.None)
                        {
                            pl.SetState(MotorFunctionPiggyback.inactiveStateID);
                        }

#if false

                            FGTLog(LogLevel.Info, base.GetType(), $"{piggi.PiggybackTarget.Status} {fg.NetObject.NetID}");

                            if (piggi.PiggybackTarget.Status == PiggybackTargetStatus.Piggybacking)
                            {
                                var riderFGCC = CGM.GetNetObjectByID(piggi.PiggybackTarget.RiderIdentifier).FGCharacterController;
                                var carrierFGCC = CGM.GetNetObjectByID(piggi.PiggybackTarget.CarrierIdentifier).FGCharacterController;

                                var riderFunc = riderFGCC.PiggybackMotorFunction;
                                var carrierFunc = carrierFGCC.PiggybackMotorFunction;

                                var anotherCrap = ConnectedPlayers[riderFGCC.NetworkID].name;

                                FGTLog(LogLevel.Info, base.GetType(), $"player with netid {fg.NetObject.NetID} ({crapName}) trying to piggyback player with netId {piggi.PiggybackTarget.RiderIdentifier} ({anotherCrap})");

                                if (carrierFunc.CanStartPiggybackWithRider(riderFGCC))
                                {
                                    carrierFunc.RequestRidePlayer(riderFGCC);
                                    riderFunc.SetFallGuyRiderMode(true);

                                    carrierFunc.GetState<MotorFunctionPiggybackStateClimbUp>()._otherFallGuy = riderFGCC;
                                    carrierFunc.GetState<MotorFunctionPiggybackStateClimbUp>()._piggybackPlayerType = PiggybackPlayerType.Carrier;
                                    carrierFunc.GetState<MotorFunctionPiggybackStateClimbUp>()._HasServerConfirmedRide_k__BackingField = true;
                                    carrierFunc.GetState<MotorFunctionPiggybackStateClimbUp>().prop_Boolean_0 = true;

                                    riderFunc.GetState<MotorFunctionPiggybackStateClimbUp>()._otherFallGuy = carrierFGCC;
                                    riderFunc.GetState<MotorFunctionPiggybackStateClimbUp>()._piggybackPlayerType = PiggybackPlayerType.Rider;
                                    riderFunc.GetState<MotorFunctionPiggybackStateClimbUp>()._HasServerConfirmedRide_k__BackingField = true;
                                    riderFunc.GetState<MotorFunctionPiggybackStateClimbUp>().prop_Boolean_0 = true;

                                    riderFunc.RequestStateChange(MotorFunctionPiggyback.climbUpStateID, MotorAgent.ResourceRequestMode.Default);
                                    carrierFunc.RequestStateChange(MotorFunctionPiggyback.climbUpStateID, MotorAgent.ResourceRequestMode.Default);

                                    //carrierFunc.GetState<MotorFunctionPiggybackStateCarrying>()._rider = riderFGCC;

                                    //riderFunc.GetState<MotorFunctionPiggybackStateRiding>()._carrierNetObject = carrierFGCC.NetObject;
                                    //riderFunc.GetState<MotorFunctionPiggybackStateRiding>()._carrierPiggybackBoneTransform = carrierFGCC.PiggybackBoneTransform;
                                    //riderFunc.GetState<MotorFunctionPiggybackStateRiding>()._carrierPlayer = carrierFGCC;

                                    //riderFunc.SetState(MotorFunctionPiggyback.ridingStateID);
                                    //carrierFunc.SetState(MotorFunctionPiggyback.carryingStateID);

                                    FGTLog(LogLevel.Info, base.GetType(), $"Player {fg.NetObject.NetID} ({crapName}) is now riding {riderFGCC.NetObject.NetID} ({anotherCrap})");
                                }
                                else
                                    FGTLog(LogLevel.Info, base.GetType(), $"piggy back not allowed on player {fg.NetObject.NetID} ({crapName}) (tried to piggy back player {riderFGCC.NetObject.NetID} ({anotherCrap}))");
#endif
                    }
                }
            }
        }

        void OnMotorTasks(GameMessageMotorAgent<NetMotorTasksSnapshot> msg, GameConnection conn)
        {
            var a = new StringBuilder();

            var fg = CGM.GetNetObjectByID(msg._netObjectID).GetComponent<FallGuysCharacterController>();
            var crapName = ConnectedPlayers[fg.NetworkID].Name;

            for (int i = 0; i < msg.NumSnapshots; i++)
            {
                var snap = msg.GetSnapShot(i);

                a.Append($"Snap {i} of player {msg._netObjectID} ({crapName})\n");

                fg.NetMotorAgentTaskReceiver?.QueueMotorTask(snap);

                ParseTasks(snap.MotorTasks, fg);
            }

            a.AppendLine();

                //FGTLog(LogLevel.Info, GetType(), a.ToString());
        }
        

        void OnPlayerSpawnedServer(MPGNetObject pNetObject, uint playerID, FG_NetworkID playerNetworkID, string accountId, string platformId, string playerName, string playerGeneratedName, uint squadId, int teamId, string partyId, int playerVsGroupId, bool tailEnabled, CustomisationSelections customisationSelections)
        {
            SpawnedPlayers++;
            pNetObject.FGCharacterController.SetupOnServer(CreateMessageSender(), null, pNetObject);
            ConnectedPlayers[playerNetworkID].NetId = pNetObject.NetID;

            if (CGM.GameRules.IsTimeAttackGameMode)
            {
                BroadcastMessage(new GameMessageServerTimeAttackRegistered()
                {
                    RemoteId = pNetObject.NetID.m_NetworkID,
                    LapState = TimeAttackLapState.NotStarted,
                    LapCount = 1,
                    MessageType = TimeAttackUpdateType.Split,
                    Deductions = new([0.0f]),
                    ElapsedTimes = new([0.0f]),
                    PauseTime = 0.0f
                });
            }

            if (SpawnedPlayers >= NetworkManager.ConnectedClients)
            {
                var msg = new GameMessageServerEventGeneric()
                {
                    Type = GameMessageServerEventGeneric.EventType.QueuedObjectsSpawned,
                };

                BroadcastMessage(msg, [ServerNetID]);
                CGMDespatcher.process(msg);

                var msg2 = new GameMessageServerEventGeneric()
                {
                    Type = GameMessageServerEventGeneric.EventType.AllPlayersSpawned,
                };

                BroadcastMessage(msg2, [ServerNetID]);
                CGMDespatcher.process(msg2);

                ServerLog("OnPlayerSpawnedServer", "We can start the intro now...");

                var msg3 = new GameMessageServerEventGeneric()
                {
                    Type = GameMessageServerEventGeneric.EventType.StartIntroCameras,
                };

                BroadcastMessage(msg3, [ServerNetID]);
                CGMDespatcher.process(msg3);
            }
        }

        internal NetworkedPlayer GetNetPlayer(MPGNetObject netObj) => ConnectedPlayers.First(x => x.Value.NetId == netObj.NetID).Value;
        internal NetworkedPlayer GetNetPlayer(MPGNetID netId)
        {
            var res = ConnectedPlayers.ToList().Find(x => x.Value.NetId == netId);
            if (res.Value != null)
                return res.Value;

            return null;
        }

        internal FG_NetworkID[] GetConnections() => [.. ConnectedPlayers.Select(x => x.Key)];

        internal void EndRound()
        {
            var resNormalList = CGM._roundResults.ToArray().ToList();
            var playersState = new Il2CppSystem.Collections.Generic.List<GameMessageServerEndRound.PerPlayerProgressState>();

            foreach (var player in CGM._clientPlayerManager._players)
            {
                if (player.objectNetID == default)
                {
                    ServerLog("EndRound", $"Skipping unknown player with default netId");
                    continue;
                }

                if (resNormalList.Find(x => x.accountID == player.accountID) != null)
                {
                    ServerLog("EndRound", $"Skipping player with account id {player.accountID} as it was already been added");

                    playersState.Add(new()
                    {
                        progressState = player.completedLevel ? PlayerProgressState.Succeeded : PlayerProgressState.Failed,
                        remotePlayerId = player.objectNetID.m_NetworkID
                    });
                    continue;
                }

                var playerInQuestion = GetNetPlayer(player.objectNetID);

                ServerManager.CGM._roundResults.Add(new RoundResult()
                {
                    wasSuccessful = player.completedLevel,
                    accountID = playerInQuestion?.AccountId,
                    netObjectID = player.objectNetID.m_NetworkID,
                    platformID = playerInQuestion?.Platform,
                    possessionScore = 0,
                    playerID = player.objectNetID.m_NetworkID,
                    teamId = player.TeamID,
                    comparisonScore = 0,
                    teamPosition = 0,
                    teamScore = 0,
                    extraDisplayInfo = default
                });

                playersState.Add(new()
                {
                    progressState = player.completedLevel ? PlayerProgressState.Succeeded : PlayerProgressState.Failed,
                    remotePlayerId = player.objectNetID.m_NetworkID
                });
            }

            BroadcastMessage(new GameMessageServerRoundResults()
            {
                roundResults = CGM._roundResults,
                _wasFinalRound = CGM.GameRules.IsFinalRound,
               _moreAreComing = false,
            });

            BroadcastMessage(new GameMessageServerEndRound()
            {
                episodeProgress = EpisodeProgressStatus.Complete,
                highestCheckpointReached = 0,
                progressStatePerPlayer = playersState,
                noNewTeamsCouldQualify = false,
            });
        }

        public MPGNetMotorAgentState.MessageSender CreateMessageSender()
        {
            void Handler(GameMessageServerMotorAgentState message, MPGNetID netObjectId, DeliveryType deliveryTypeToLocalClient, DeliveryType deliveryTypeToRemoteClient, Il2CppStructArray<FG_NetworkID> excludedRecipientIDs, int amountExcluded)
            {
                    //FGTLog(BepInEx.Logging.LogLevel.Error, GetType(), $"Sending motor agent state for {netObjectId}");

                BroadcastMessage(message, [ServerNetID]);
            }

            return DelegateSupport.ConvertDelegate<MPGNetMotorAgentState.MessageSender>(Handler);
        }

        public MPGNetMotorAgentTaskReceiver.MotorTasksAppliedCallback CreateMotorCallback(MPGNetID netId)
        {
            void Handler(int numApplied, float lastAppliedTimestamp)
            {

            }

            return DelegateSupport.ConvertDelegate<MPGNetMotorAgentTaskReceiver.MotorTasksAppliedCallback>(Handler);
        }

        static GameMessageServerSpawnObject CreatePlayerSpawnRequest(GameConnection conn, uint pid, ref NetworkedPlayer player)
        {
            var netid = new MPGNetID(pid);
            var msg = new GameMessageServerSpawnObject()
            {
                _netObjectSpawnData = new()
                {
                    _netID = netid,
                    _creationMode = NetObjectCreationMode.Spawn,
                    _lodControllerBehaviour = FG.Common.LODs.LodController.LodControllerBehaviour.Default,
                    _scale = Vector3.one,
                    _spawnObjectType = EnumSpawnObjectType.PLAYER,
                    _prefabHash = PlayerHash,
                    _syncScale = false,
                    _syncTransform = true,
                    _position = Vector3.zero,
                    _rotation = Quaternion.identity,
                    _postSpawnAction = new Action<MPGNetID, GameObject>((MPGNetID netId, GameObject obj) =>
                    {

                    }),
                    _additionalSpawnData = new PlayerSpawnData()
                    {
                        _customisationSelections = player.Cosmetics,
                        _accessoryEnabled = false,
                        _accountId = player.AccountId,
                        _partyId = null,
                        _platformAccountName = player.Name,
                        _platformId = player.Platform,
                        _playerGeneratedName = "Internal Name",
                        _playerId = pid,
                        _playerNetworkId = new(conn.RemoteNetworkID._networkID),
                        _squadId = 0,
                        _teamId = -1,
                        _vsGroupId = 0,
                    }
                },
            };

            return msg;
        }

        public void Shutdown()
        {
            HandleServerState(ServerState.Closing);

            PendingConnections.Clear();
            ConnectedPlayers.Clear();
            PlayerSpawnQueue.Clear();

            GlobalGameStateClient.Instance.NetObjectManager.OnSpawnNetObject -= OnSpawnNetObj;
            OnPlayerSpawned -= OnPlayerSpawnedServer;

            ServerDespatcher.OnPing -= OnPing;
            ServerDespatcher.OnClientConnectInitial -= AuthPlayer;
            ServerDespatcher.OnClientConnectClient -= FG_ClientConnectRequest;
            ServerDespatcher.OnSetReady -= OnSetReady;
            ServerDespatcher.OnSpawnPlayer -= SpawnPlayer;
            ServerDespatcher.OnMotorTasks -= OnMotorTasks;
            ServerDespatcher.OnDisconnectPlayer -= OnDisconnect;
            ServerDespatcher.OnTimeAttackReset -= OnTimeAttackReset;

            CustomMessageDespatcher.OnServerConnectRequest -= CustomMessageDespatcher_ClientConnectRequest;
            CustomMessageDespatcher.OnServerUserInfo -= CustomMessageDespatcher_OnClientUserInfo;

            Commands.OnRoundLoaded -= OnRoundLoaded;
            Commands.OnCheckpointReached -= OnCheckpointReached;

            COMMON_ObjectiveBase.m_OnObjectiveSatisfied_SERVERONLY = null;

            TimeAttackStart -= OnTimeAttackStart;
            TimeAttackFinish -= OnTimeAttackEnd;

            ServerLog("Shutdown", "Shutdown completed!");
        }

        internal void OnNetObjSpawned(MPGNetObject mpg)
        {
            ServerLog("OnNetObjSpawned", $"spawned {mpg.name}");
        }

        void ServerLog(string method, string msg) => FGTLog(LogLevel.Info, $"{typeof(ServerManager).Name} - {method}", msg);

        public void BroadcastMessage<T>(T msg, FG_NetworkID[] except = null) where T : GameMessageBase
        {
            if (State == ServerState.Closing)
                return;

            if (msg.GetActualType() != typeof(GameMessageServerMotorAgentState))
               ServerLog("BroadcastMessage", $"Attempt to broadcast msg of type [{msg.GetIl2CppType().Name}]...");

            var msgDat = FG_NetworkManager.SerializeAndDeallocateGameMessage(msg);

            foreach (var conn in NetworkManager.AllGameConnections())
            {
                if (except != null && except.Contains(conn.RemoteNetworkID))
                    continue;

                try
                {
                    conn.SendReliable(msgDat);

                    if (msg.GetActualType() != typeof(GameMessageServerMotorAgentState))
                        ServerLog("BroadcastMessage", $"Sent {msg.GetActualType().Name} to {conn.RemoteNetworkID} (our client: {conn.RemoteNetworkID == ServerNetID})");
                }
                catch (Exception ex)
                {
                    FGTLog(BepInEx.Logging.LogLevel.Error, GetType(), ex.Message);
                }

            }

        }

        public void BroadcastMessage(Il2CppSystem.ArraySegment<byte> content, FG_NetworkID[] except = null)
        {
            if (State == ServerState.Closing)
                return;

                ServerLog("BroadcastMessage", $"Attempt to broadcast content length [{content.Array.Length}]...");

            var allConns = NetworkManager.AllGameConnections();

            foreach (var conn in allConns)
            {
                if (except != null && except.Contains(conn.RemoteNetworkID))
                    continue;

                try
                {
                    conn.SendReliable(content);

                        ServerLog("BroadcastMessage", $"Sent content length {content.Array.Length} to {conn.RemoteNetworkID} (our client: {conn.RemoteNetworkID == ServerNetID})");
                }
                catch (Exception ex)
                {
                    FGTLog(BepInEx.Logging.LogLevel.Error, GetType(), ex.Message);
                }

            }

        }
    }
}
