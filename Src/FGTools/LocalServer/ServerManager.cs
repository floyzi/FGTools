extern alias wle;

using BepInEx.Logging;
using FG.Common;
using FG.Common.Character;
using FG.Common.Character.MotorSystem;
using FG.Common.CMS;
using FG.Common.Fraggle;
using FG.Common.LODs;
using FG.Common.Messages;
using FG.Common.Network;
using FGClient;
using FGClient.UI;
using FGTools.Internal;
using FGTools.Internal.Behaviours;
using FGTools.Internal.Behaviours.ServerSide;
using FGTools.Internal.Extensions;
using FGTools.LocalServer.CustomMessages;
using FGTools.LocalServer.CustomMessages.Logic;
using FGTools.Services;
using FGTools.States.Logic;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppSystem.Linq;
using Levels.HexARing;
using Levels.HexSnake;
using Levels.Obstacles;
using Levels.PixelPerfect;
using Levels.Progression;
using Levels.Rollout;
using Levels.ScoreZone;
using Levels.TimeAttack;
using Mediatonic.Networking;
using SRF;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.SocialPlatforms.Impl;
using UniverseLib;
using static FG.Common.COMMON_ObjectiveBase;
using static FG.Common.FG_NetworkManager;
using static FGTools.Internal.Extensions.FLZ_Extensions;
using Random = UnityEngine.Random;

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
            GameEnded,
            Closing
        }


        readonly NetworkRequest Server;
        readonly LocalServerService ServerService;
        internal readonly FG_NetworkManager NetworkManager;
        readonly int LobbySize;
        readonly object GameInfo;
        internal FG_NetworkID ServerNetID;
        string NextRound;
        int NextSeed;
        internal ServerState State = ServerState.Disconnected;

        readonly Queue<GameMessageServerSpawnObject> PlayerSpawnQueue;
        readonly Dictionary<FG_NetworkID, NetworkedPlayer> ConnectedPlayers;
        readonly Dictionary<FG_NetworkID, GameConnection> PendingConnections;

        int SpawnedPlayers = 0, ReadyPlayers = 0, LoadedPlayers = 0;

        internal static Action<MPGNetObject> TimeAttackStart;
        internal static Action<MPGNetObject> TimeAttackFinish;
        internal static Action<MPGNetObject, uint, FG_NetworkID, string, string, string, string, uint, int, string, int, bool, CustomisationSelections> OnPlayerSpawned;
        internal static Action<NetworkMessage> OnServerReceivedMessage;

        public ServerManager(LocalServerService service, NetworkRequest startTicket, FG_NetworkManager netManager, object gameInfo, int lobbySize = 1)
        {
            ServerService = service;
            Server = startTicket;
            NetworkManager = netManager;
            LobbySize = lobbySize;
            ServerNetID = GlobalGameStateClient.Instance.GetLocalClientNetworkID();

            if (GameMessageFactory._messagePool == null)
                GameMessageFactory.Initialize(true);

            OnPlayerSpawned += OnPlayerSpawnedServer;

            ServerDespatcher.OnPing += OnPing;
            ServerDespatcher.OnClientConnectInitial += AuthPlayer;
            ServerDespatcher.OnClientConnectClient += FG_ClientConnectRequest;
            ServerDespatcher.OnSetReady += OnSetReady;
            ServerDespatcher.OnSpawnPlayer += SpawnPlayer;
            ServerDespatcher.OnMotorTasks += OnMotorTasks;
            ServerDespatcher.OnDisconnectPlayer += OnDisconnect;
            ServerDespatcher.OnTimeAttackReset += OnTimeAttackReset;
            ServerDespatcher.OnResetToCheckpoint += OnRequestRespawn;
            ServerDespatcher.OnSkipRound += OnRequestNewRound;

            CustomMessageDespatcher.OnServerConnectRequest += CustomMessageDespatcher_ClientConnectRequest;
            CustomMessageDespatcher.OnServerUserInfo += CustomMessageDespatcher_OnClientUserInfo;

            GameActions.OnCheckpointReached += OnCheckpointReached;
            GameActions.OnIntroEnds += OnIntroEnd;
            GameActions.OnRoundStarts += OnRoundStart;
            GameActions.OnIntroStarts += OnIntroStarts;
            GameActions.OnNetObjSpawned += OnNetObjSpawned;
            GameActions.OnRoundEnds += OnRoundEnds;


            COMMON_ObjectiveBase.m_OnObjectiveSatisfied_SERVERONLY = DelegateSupport.ConvertDelegate<HandleObjectiveSatisfied>(ObjectiveAchived);

            TimeAttackStart += OnTimeAttackStart;
            TimeAttackFinish += OnTimeAttackEnd;

            GameInfo = gameInfo;
            PlayerSpawnQueue = [];
            ConnectedPlayers = [];
            PendingConnections = [];

            foreach (var fgcc in Resources.FindObjectsOfTypeAll<FallGuysCharacterController>())
                fgcc.MotorAgent._motorFunctionsConfig = MotorAgent.MotorAgentConfiguration.Offline;

            GameActions.OnRoundLoaded += PrepareForNetworkedGame;

            HandleServerState(ServerState.Open);
        }

        void OnIntroEnd()
        {
        }

        void OnRoundStart()
        {
            GameObject[] possibleTargets;
            var netObjects = Resources.FindObjectsOfTypeAll<MPGNetObjectBase>().Select(obj => obj.gameObject);
            var movableObjects = Resources.FindObjectsOfTypeAll<wle.LevelEditorMovableObject>().Select(obj => obj.gameObject);
            possibleTargets = [.. netObjects, .. movableObjects];

            foreach (GameObject obj in possibleTargets)
            {
                if (obj.GetComponent<OfflineGrabTargetID>() == null)
                {
                    var targ = obj.gameObject.AddComponent<OfflineGrabTargetID>();
                    targ._hashID = (uint)Random.Range(10000, 99999);
                    targ.Type = OfflineGrabTargetID.OfflineGrabTargetIDType.Grab | OfflineGrabTargetID.OfflineGrabTargetIDType.Mantle;
                }
            }


            var ppm = Resources.FindObjectsOfTypeAll<PixelPerfectManager>().FirstOrDefault();
            ppm?.Init();
            ppm?.BeginGame();

            ScoreZoneManager.NumPlayers = FGTServiceManager.GetService<RoundOptionsService>().GetPlayers();
            foreach (var zone in Resources.FindObjectsOfTypeAll<ScoreZoneManager>())
            {
                //slop
                if (SceneManager.GetActiveScene().name.EndsWith("FollowTheLeader"))
                {
                    zone._scoreZones = new(Resources.FindObjectsOfTypeAll<ScoreZone>());
                    Resources.FindObjectsOfTypeAll<VolumeZoneTrigger>().FirstOrDefault()._volumeZone = Resources.FindObjectsOfTypeAll<VolumeZone>().FirstOrDefault();
                    zone?.OnGameStart();
                    continue;
                }

                foreach (var z in zone._scoreZones)
                {
                    if (z.GetIl2CppType() != Il2CppType.Of<CollectionZone>())
                        continue;

                    foreach (var c in z.Cast<CollectionZone>()._collectables)
                    {
                        c.Hide();
                    }
                }

                zone?.InitZones();
                zone?.ActivateInitialZones();
                zone?.OnGameStart();
            }
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
                EpisodeGuid = Il2CppSystem.Guid.NewGuid(),
                IsCrossPlatform = new Il2CppSystem.Nullable<bool>(true),
                PlayerIds = new Il2CppStructArray<uint>([1]),
                ResponseCode = GameMessageServerConnectedClient.EnumConnectionResponse.ECT_SUCCESSFUL_PARTICIPANT,
                ServerBuildInfo = "local_fgt_server",
                ServerId = 102,
                ServerTime = Il2CppSystem.DateTime.UtcNow,
                //i need to be better than this
                ShowId = StateManager.IsPlayingExplore ? "casual_show" : "classic_solo_main_show",
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

            //playerData.GetCurrentLap.ElapsedTime.Add(playerData.GetCurrentLap.CurrentLapTimeUpToSplit(0));

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
            foreach (var rl in Resources.FindObjectsOfTypeAll<RolloutManager>().ToList().FindAll(x => x.gameObject.activeInHierarchy))
            {
                var res = new Il2CppSystem.Collections.Generic.List<int>();
                int ringSchemas = rl._ringSegmentSchemas.Count;
                int ringLimit = UnityEngine.Random.Range(2, ringSchemas);
                int addedRings = 0;

                for (int i = 0; i < ringSchemas; i++)
                {
                    if (addedRings < ringSchemas)
                    {
                        int ringMax = UnityEngine.Random.Range(0, rl._ringSegmentSchemas[i].PrefabPool.Count);
                        res.Add(ringMax);
                        addedRings++;
                    }
                }

                rl.SetSelectedPrefabIndexes(res);
                ServerGameStateActions.Instance.SetupNetworkObject(rl.GetComponent<MPGNetObjectPossessable>(), rl.transform.position, rl.transform.rotation, rl.transform.localScale, null);
            }

            foreach (var hm in Resources.FindObjectsOfTypeAll<HexARingManager>().ToList().FindAll(x => x.gameObject.activeInHierarchy))
            {
                hm.SetPlayerCount(FGTServiceManager.GetService<RoundOptionsService>().GetPlayers());
            }

            foreach (var hm in Resources.FindObjectsOfTypeAll<HexSnakeManager>().ToList().FindAll(x => x.gameObject.activeInHierarchy))
            {
                hm.UpdatePlayerCount(FGTServiceManager.GetService<RoundOptionsService>().GetPlayers());
            }

            foreach (PlayerRatioedBulkItemSpawner spawner in Resources.FindObjectsOfTypeAll<PlayerRatioedBulkItemSpawner>())
            {
                var itmCount = Mathf.Clamp(Mathf.RoundToInt(FGTServiceManager.GetService<RoundOptionsService>().GetPlayers() * spawner._numberOfItemsPerPlayer), spawner._minItems, spawner._maxItems);

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
                        var c = child.childCount;
                        if (child.childCount > 0)
                        {
                            for (int j = 0; j < c; j++)
                                spawn.Add(child.GetChild(j));
                        }
                        else
                            spawn.Add(child);

                        spawns.Add(child.gameObject, spawn);
                    }
                }

                foreach (var pair in spawns)
                {
                    //for (int j = 0; j < itmCount / spawns.Count && j < pair.Value.Count; j++)
                    for (int j = 0; j < pair.Value.Count && itmCount > 0; j++)
                    {
                        var targetObject = spawner.ItemPrefab.GetComponent<NetworkAwareGeneric>().SpawnObject;

                        if (!targetObject.TryGetComponent<MPGNetObject>(out var netObj))
                            netObj = targetObject.gameObject.AddComponent<MPGNetObject>();

                        targetObject.RemoveComponentIfExists<NetworkAwareGeneric>();
                        netObj.NetID = GlobalGameStateClient.Instance.NetObjectManager.GetNextNetID();
                        netObj.GameObjectHash = netObj.GenerateGameObjectHash(NetObjectCreationMode.Spawn);

                        var spawn = pair.Value[j];
                        netObj.SpawnPrefab(spawn.position, spawn.rotation, spawn.localScale);

                        itmCount--;
                    }
                }

            }

            if (StateManager.IsFGC)
            {
                foreach (var kz in Resources.FindObjectsOfTypeAll<COMMON_PlayerEliminationVolume>().ToList().FindAll(x => x.gameObject.activeInHierarchy))
                {
                    var st = kz._collisionVolume.gameObject.AddComponent<SimpleTrigger>();

                    st.TriggerEnter += new Action<Collider>(other => kz.OnTriggerEnter(other));
                    st.CollisionEnter += new Action<Collision>(other => kz.OnCollisionEnter(other));
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
            ServerLog("ObjectiveAchived", $"Objective achived by object {playerObjectNetID} (objective {pObjective.GetIl2CppType().Name}");

            if (pObjective.TryGetComponent<Animation>(out var anim))
                anim.enabled = false;

            ServerGameStateActions.Instance.MarkPlayerAsSuccessful(CGM.GetNetObjectByID(playerObjectNetID), pObjective.GetIl2CppType() != Il2CppType.Of<COMMON_GrabToQualify>());
        }

        readonly static List<Il2CppSystem.Type> NotForUnifiedSetup = 
        [
            Il2CppType.Of<HexARingManager>()
        ];

        internal static bool UseUnifiedSetup(MPGNetObjectBase based)
        {
            foreach (var p in NotForUnifiedSetup)
            {
                if (based.GetComponent(p) != null)
                    return false;
            }    

            return true;
        }

        internal void OnServerSpawnedObject(GameObject obj, MPGNetID net)
        {
            Debug.Log("spawned " + obj.name);
            if (obj.TryGetComponent<COMMON_GrabToQualify>(out var gtq))
            {
                gtq.GetComponent<Animation>().enabled = true;

                var ogt = gtq.gameObject.AddComponent<OfflineGrabTargetID>();
                ogt._hashID = (uint)Random.Range(10000, 99999);
                ogt.Type = OfflineGrabTargetID.OfflineGrabTargetIDType.Grab | OfflineGrabTargetID.OfflineGrabTargetIDType.Mantle;
            }
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


        void FG_ClientConnectRequest(Il2CppSystem.Object msg, GameConnection playerConn)
        {
            if (State == ServerState.Closing)
                return;

            if (LocalServerService.GameMessageClientConnectClientType == null)
            {
                FGTServiceManager.GetService<LocalServerService>().KillServer(new Exception($"Obfuscated il2cpp client connect client message type was null during {nameof(FG_ClientConnectRequest)}"));
                return;
            }

            var field = LocalServerService.GameMessageClientConnectClientType.GetFields().FirstOrDefault(x => x.FieldType == Il2CppType.Of<FG_NetworkID>());
            playerConn.RemoteNetworkID = field.GetValue(msg).Unbox<FG_NetworkID>();
            Console.WriteLine(playerConn.RemoteNetworkID);
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

            var servVer = new System.Version(Launcher.BuildInfo.Version);
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

            if (clientVer == servVer && msg.ID != Launcher.BuildInfo.GUID)
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

            nextRound.GameRules.StartGameMessage = new()
            {
                Title = FLZ_Extensions.AddCMSString("gameplay_warn_title", LocalizationService.LocalizedStr("pre_release_warn_title")),
                Body = FLZ_Extensions.AddCMSString("gameplay_warn_desc", LocalizationService.LocalizedStr("pre_release_warn_desc"))
            };
            nextRound.GameRules.StartGameMessageTrigger = StartGameMessageTrigger.Intro;
            nextRound.GameRules.StartGameMessageFormat = UIOverlayMessageFormat.Popup;

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
            BroadcastMessage(new GameMessageServerStartLoadingLevel());
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
                CGM.GameRules.PreparePlayerStartingPositions(LobbySize);

                while (PlayerSpawnQueue.Count > 0)
                {
                    var spawnAct = PlayerSpawnQueue.Dequeue();
                    try
                    {
                        var allPositions = Resources.FindObjectsOfTypeAll<MultiplayerStartingPosition>();
                        var pos = CGM.GameRules._playerStartingPositions.Count > 0 ? CGM.GameRules.PickStartingPosition((int)spawnAct.NetId.m_NetworkID, 0, -1, 0, false) : allPositions[UnityEngine.Random.Range(0, allPositions.Count)];

                        spawnAct._netObjectSpawnData._position = pos.transform.position;
                        spawnAct._netObjectSpawnData._rotation = pos.transform.rotation;

                        GlobalGameStateClient.Instance.NetObjectManager.SpawnNetObject(spawnAct);
                        BroadcastMessage(spawnAct, [ServerNetID]);

                        ServerLog("TryToPerfomSpawns", $"Broadcasted spawn for player {spawnAct.NetId}... Remain {PlayerSpawnQueue.Count}");
                    }
                    catch (Exception e)
                    {
                        FGTLog(LogLevel.Error, GetType(), $"Spawn of player failed!!!");
                        ServerService.KillServer(e);
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
                    {
                        player.gameObject.AddComponent<ServerControlledObject>();
                        player.MotorAgent.IsGameServer = true;
                    }

                    player.SpeedBoostManager._isAuthoritative = true;
                }

                RMIBehaviourManager.SetActive();

                if (CGM.IsUGCRound)
                {
                    //better to be temp
                    FraggleCommonManager.Instance.IsInLevelEditor = true;
                    FraggleCommonManager.Instance.SetModeToExplore(new());
                }

                BroadcastMessage(new GameMessageServerQualificationProgressUpdated()
                {
                    NumQualifiedPlayers = 0,
                    NumEliminatedPlayers = 0,
                });

                BroadcastMessage(new GameMessageServerStartGame()
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
                });

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

        void OnRequestRespawn(GameMessageClientResetToCheckpoint msg, GameConnection conn)
        {
            var player = CGM.GetNetObjectByID(ConnectedPlayers[conn.RemoteNetworkID].NetId).FGCharacterController;
            ServerGameStateActions.Instance.RespawnParticipant(player);
        }

        //TEMP
        void OnRequestNewRound(GameMessageClientSkipRound msg, GameConnection conn)
        {
            var player = CGM.GetNetObjectByID(ConnectedPlayers[conn.RemoteNetworkID].NetId);
            ServerGameStateActions.Instance.RequestDestroy(player);
            AudioManager.PlayGameplayEndAudio(true);

            QualifiedScreenViewModel.Show("skipped", new Action(() =>
            {
                if (StateManager.IsPlayingExplore)
                    StateManager.ExploreState.RequestNewRound();
                else
                    FLZ_Extensions.ForceExit();
            }));
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
                BroadcastMessage(new GameMessageServerEventGeneric()
                {
                    Type = GameMessageServerEventGeneric.EventType.QueuedObjectsSpawned,
                });

                BroadcastMessage(new GameMessageServerEventGeneric()
                {
                    Type = GameMessageServerEventGeneric.EventType.AllPlayersSpawned,
                });

                ServerLog("OnPlayerSpawnedServer", "We can start the intro now...");

                BroadcastMessage(new GameMessageServerEventGeneric()
                {
                    Type = GameMessageServerEventGeneric.EventType.StartIntroCameras,
                });
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

        internal void EndRound(bool allowPlayerQual)
        {
            if (State == ServerState.GameEnded)
                return;

            HandleServerState(ServerState.GameEnded);

            var resNormalList = CGM._roundResults.ToArray().ToList();
            var playersState = new Il2CppSystem.Collections.Generic.List<GameMessageServerEndRound.PerPlayerProgressState>();
            var progressQueue = new Queue<Action>();
            foreach (var player in CGM._clientPlayerManager._players)
            {
                var didPass = CGM.GameRules.IsSurvivalRound || player.completedLevel;

                if (player.objectNetID == default)
                {
                    ServerLog("EndRound", $"Skipping unknown player with default netId");
                    continue;
                }

                var playerObj = CGM.GetNetObjectByID(player.objectNetID);

                if (resNormalList.Find(x => x.accountID == player.accountID) != null)
                {
                    ServerLog("EndRound`", $"Skipping player with account id {player.accountID} as it was already been added");

                    playersState.Add(new()
                    {
                        progressState = didPass ? PlayerProgressState.Succeeded : PlayerProgressState.Failed,
                        remotePlayerId = player.objectNetID.m_NetworkID
                    });
                    continue;
                }

                if (allowPlayerQual)
                {
                    progressQueue.Enqueue(() =>
                    {
                        if (didPass)
                            ServerGameStateActions.Instance.MarkPlayerAsSuccessful(playerObj, false);
                        else
                            ServerGameStateActions.Instance.EliminateParticipant(playerObj, false, LiveOps.Challenges.EliminationReason.None);
                    });
                }

                playersState.Add(new()
                {
                    progressState = didPass ? PlayerProgressState.Succeeded : PlayerProgressState.Failed,
                    remotePlayerId = player.objectNetID.m_NetworkID
                });
            }

            BroadcastMessage(new GameMessageServerRoundResults()
            {
                roundResults = CGM._roundResults,
                _wasFinalRound = CGM.GameRules.IsFinalRound,
               _moreAreComing = false,
            });

            //REWORK
            for (int i = 0; i < progressQueue.Count; i++)
            {
                progressQueue.Dequeue().Invoke();
            }

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

        //public MPGNetMotorAgentTaskReceiver.MotorTasksAppliedCallback CreateMotorCallback(MPGNetID netId)
        //{
        //    void Handler(int numApplied, float lastAppliedTimestamp)
        //    {

        //    }

        //    return DelegateSupport.ConvertDelegate<MPGNetMotorAgentTaskReceiver.MotorTasksAppliedCallback>(Handler);
        //}

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

            OnPlayerSpawned -= OnPlayerSpawnedServer;

            ServerDespatcher.OnPing -= OnPing;
            ServerDespatcher.OnClientConnectInitial -= AuthPlayer;
            ServerDespatcher.OnClientConnectClient -= FG_ClientConnectRequest;
            ServerDespatcher.OnSetReady -= OnSetReady;
            ServerDespatcher.OnSpawnPlayer -= SpawnPlayer;
            ServerDespatcher.OnMotorTasks -= OnMotorTasks;
            ServerDespatcher.OnDisconnectPlayer -= OnDisconnect;
            ServerDespatcher.OnTimeAttackReset -= OnTimeAttackReset;
            ServerDespatcher.OnResetToCheckpoint -= OnRequestRespawn;
            ServerDespatcher.OnSkipRound -= OnRequestNewRound;

            CustomMessageDespatcher.OnServerConnectRequest -= CustomMessageDespatcher_ClientConnectRequest;
            CustomMessageDespatcher.OnServerUserInfo -= CustomMessageDespatcher_OnClientUserInfo;

            GameActions.OnRoundLoaded -= PrepareForNetworkedGame;
            GameActions.OnCheckpointReached -= OnCheckpointReached;
            GameActions.OnIntroEnds -= OnIntroEnd;
            GameActions.OnIntroStarts -= OnIntroStarts;
            GameActions.OnRoundStarts -= OnRoundStart;
            GameActions.OnNetObjSpawned -= OnNetObjSpawned;
            GameActions.OnRoundEnds -= OnRoundEnds;


            COMMON_ObjectiveBase.m_OnObjectiveSatisfied_SERVERONLY = null;

            TimeAttackStart -= OnTimeAttackStart;
            TimeAttackFinish -= OnTimeAttackEnd;

            ServerLog("Shutdown", "Shutdown completed!");
        }

        void OnIntroStarts()
        {
        }

        void OnRoundEnds()
        {
        }

        internal void OnNetObjSpawned(MPGNetID netId, GameObject mpg, int hash)
        {
            ServerLog("OnNetObjSpawned", $"spawned {mpg.name}");

            if (mpg.TryGetComponent<COMMON_GrabToQualify>(out var gtq))
            {
                if (gtq.TryGetComponent<Animation>(out var anim))
                    anim.enabled = true;

                var ogt = gtq.gameObject.AddComponent<OfflineGrabTargetID>();
                ogt._hashID = (uint)Random.Range(10000, 99999);
                ogt.Type = OfflineGrabTargetID.OfflineGrabTargetIDType.Grab | OfflineGrabTargetID.OfflineGrabTargetIDType.Mantle;
                gtq.enabled = true;
            }
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
