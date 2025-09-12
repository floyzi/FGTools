using BepInEx.Logging;
using FG.Common;
using FG.Common.Character;
using FG.Common.Character.MotorSystem;
using FG.Common.LODs;
using FG.Common.Messages;
using FG.Common.Network;
using FGClient;
using FGTools.Config;
using FGTools.Internal.Behaviours;
using FGTools.Internal.Extensions;
using FGTools.Services;
using FGTools.Services.Logic;
using FGTools.States.Logic;
using Il2CppInterop.Runtime.Attributes;
using Il2CppInterop.Runtime.Injection;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppSystem.Linq;
using Levels.Obstacles;
using Levels.Rollout;
using LiveOps.Challenges;
using SRF;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using static FGClient.UI.MultiRowMenuInputHandler;
using static LiveOps.Collections.CollectableZone;
using Debug = UnityEngine.Debug;

namespace FGTools.LocalServer.Implementations
{
    [Il2CppImplements(typeof(IGameStateServerActions))]
    public class ServerGameActions : Il2CppSystem.Object
    {
        public ServerGameActions() : base(ClassInjector.DerivedConstructorPointer<ServerGameActions>())
        {
            ClassInjector.DerivedConstructorBody(this);
        }

        void MarkPlayerAsSuccessful(MPGNetObject playerNetObject, bool shouldDespawn)
        {
            if (LocalServerService.CGM.GameRules.IsTimeAttackGameMode)
                return;

            FLZ_Extensions.FGTLog(LogLevel.Warning, "MarkPlayerAsSuccessful", $"Trying to qualify NetObject {playerNetObject.name} with ID {playerNetObject.NetID}");

            if (LocalServerService.IsUserAloneAndHost && ConfigManager.QualLevel.Value == ConfigManager.QualType.None)
            {
                FLZ_Extensions.FGTLog(LogLevel.Info, "MarkPlayerAsSuccessful", $"Qualifications disabled.");
                return;
            }

            var playerInQuestion = LocalServerService.ServerManager.GetNetPlayer(playerNetObject);
            var spS = FGTServiceManager.Instance.GetService<SpeedrunService>();
            bool isNotInSpeedrun = LocalServerService.IsUserAloneAndHost && !ConfigManager.SpeedrunMode.Value || spS.IsSepeedrunsDisabled;
            var playerData = ServerManager.CGM.GetPlayerData(playerNetObject.NetID);
            var actualFinal = ServerManager.CGM._round.GameRules.IsFinalRound && isNotInSpeedrun;

            if (playerData.completedLevel)
            {
                FLZ_Extensions.FGTLog(LogLevel.Info, "MarkPlayerAsSuccessful", $"Player already qualified.");
                return;
            }

            ServerManager.CGM._roundResults.Add(new RoundResult()
            {
                wasSuccessful = true,
                accountID = playerInQuestion.AccountId,
                netObjectID = playerNetObject.NetID.m_NetworkID,
                platformID = playerInQuestion.Platform,
                possessionScore = 0,
                playerID = playerNetObject.NetID.m_NetworkID,
                teamId = playerInQuestion.TeamID,
                comparisonScore = 0,
                teamPosition = 0,
                teamScore = 0,
                extraDisplayInfo = default
            });

            if (isNotInSpeedrun)
                ServerManager.CGM._qualifiedPlayerCount++;

            ServerManager.CGM._clientPlayerManager.GetClientPlayerDataForNetId(playerNetObject.NetID).completedLevel = true;

            var roundEndCondition = ServerManager.CGM.QualifiedPlayerCount >= ServerManager.CGM.RequiredQualifiedPlayerCount && LocalServerService.ServerManager.State != ServerManager.ServerState.GameEnded;
            
            if (roundEndCondition && actualFinal)
                LocalServerService.ServerManager.EndRound(true);
            else if (!actualFinal)
            {
                LocalServerService.ServerManager.BroadcastMessage(new GameMessageServerPlayerProgress()
                {
                    isFinal = actualFinal,
                    playerId = playerNetObject.NetID.m_NetworkID,
                    progressCause = GameMessageServerPlayerProgress.ProgressCause.Individual,
                    succeeded = true,
                });
            }

            LocalServerService.ServerManager.BroadcastMessage(new GameMessageServerQualificationProgressUpdated()
            {
                NumQualifiedPlayers = (uint)ServerManager.CGM._qualifiedPlayerCount,
                NumEliminatedPlayers = (uint)ServerManager.CGM._eliminatedPlayerCount,
            });

            if (isNotInSpeedrun)
            {
                if (roundEndCondition)
                    LocalServerService.ServerManager.EndRound(true);

                if (shouldDespawn)
                    RequestDestroy(playerNetObject);
            }
        }

        void MarkTeamAsSuccessful(int teamId, bool shouldDespawn)
        {
            throw new NotImplementedException();
        }

        void EliminateParticipant(MPGNetObject playerNetObject, bool isCheater, EliminationReason eliminationReason)
        {
            FLZ_Extensions.FGTLog(BepInEx.Logging.LogLevel.Warning, "EliminateParticipant", $"Trying to eliminate NetObject {playerNetObject.name} with ID {playerNetObject.NetID}");

            if (LocalServerService.IsUserAloneAndHost && ConfigManager.ElimLevel.Value == ConfigManager.ElimType.None)
                return;

            var spS = FGTServiceManager.Instance.GetService<SpeedrunService>();
            bool isNotInSpeedrun = LocalServerService.IsUserAloneAndHost && !ConfigManager.SpeedrunMode.Value || spS.IsSepeedrunsDisabled;
            var playerInQuestion = LocalServerService.ServerManager.GetNetPlayer(playerNetObject);
            var playerData = ServerManager.CGM.GetPlayerData(playerNetObject.NetID);

            ServerManager.CGM._roundResults.Add(new RoundResult()
            {
                wasSuccessful = false,
                accountID = playerInQuestion.AccountId,
                netObjectID = playerNetObject.NetID.m_NetworkID,
                platformID = playerInQuestion.Platform,
                possessionScore = 0,
                playerID = playerNetObject.NetID.m_NetworkID,
                teamId = playerData.TeamID,
                comparisonScore = 0,
                teamPosition = 0,
                teamScore = 0,
                extraDisplayInfo = default
            });

            if (isNotInSpeedrun)
                ServerManager.CGM._eliminatedPlayerCount++;

            LocalServerService.ServerManager.BroadcastMessage(new GameMessageServerPlayerProgress()
            {
                isFinal = ServerManager.CGM._round.GameRules.IsFinalRound,
                playerId = playerNetObject.NetID.m_NetworkID,
                progressCause = GameMessageServerPlayerProgress.ProgressCause.Individual,
                succeeded = false,
            });

            LocalServerService.ServerManager.BroadcastMessage(new GameMessageServerQualificationProgressUpdated()
            {
                NumQualifiedPlayers = (uint)ServerManager.CGM._qualifiedPlayerCount,
                NumEliminatedPlayers = (uint)ServerManager.CGM._eliminatedPlayerCount,
            });

            if (isNotInSpeedrun)
            {
                if (ServerManager.CGM.EliminatedPlayerCount >= ServerManager.CGM.RequiredEliminatedPlayerCount && LocalServerService.ServerManager.State != ServerManager.ServerState.GameEnded)
                    LocalServerService.ServerManager.EndRound(true);

                RequestDestroy(playerNetObject);
            }
        }

        void RequestDestroy(MPGNetObject go)
        {
            if (go == null || go.NetID == default)
                return;

            FLZ_Extensions.FGTLog(BepInEx.Logging.LogLevel.Warning, "RequestDestroy", $"Trying to destroy NetObject {go.name} with ID {go.NetID}");

            LocalServerService.ServerManager.BroadcastMessage(new GameMessageServerUnspawnObject()
            {
                m_netID = go.NetID,
            });

            GlobalGameStateClient.Instance.NetObjectManager.UnspawnNetObject(go.NetID, MPGNetObjectManager.UnspawnGameObjectPolicy.Destroy);
        }

        void SetupNetworkObject(MPGNetObjectBase netObjectBase, Vector3 spawnPosition, Quaternion spawnRotation, Vector3 spawnScale, Il2CppSystem.Action<MPGNetID, GameObject> PostSpawnAction)
        {
            FLZ_Extensions.FGTLog(BepInEx.Logging.LogLevel.Warning, "SetupNetworkObject", $"Possessing net object \"{netObjectBase.name}\"");

            var hash = (uint)netObjectBase.IdentifyingHash();
            var unified = ServerManager.UseUnifiedSetup(netObjectBase);

            FLZ_Extensions.FGTLog(BepInEx.Logging.LogLevel.Debug, "SetupNetworkObject", $"IsUnified={unified}");

            var spawnData = new NetObjectSpawnData()
            {
                Position = spawnPosition,
                Rotation = spawnRotation,
                Scale = spawnScale, 
                _spawnObjectType = netObjectBase.SpawnObjectType(),
                _creationMode = netObjectBase.CreationMode(),
                _prefabHash = (int)hash,
                _useUnifiedSetup = false,
                _lodControllerBehaviour = netObjectBase.LodControllerBehaviour,
                _netID = GlobalGameStateClient.Instance.NetObjectManager.GetNextNetID(),
                _rmiIdentifier = RMIBehaviourManager.GetNextID(),
                _postSpawnAction = new Action<MPGNetID, GameObject>((MPGNetID netid, GameObject obj) =>
                {
                    LocalServerService.ServerManager.OnServerSpawnedObject(obj, netid);
                    //PostSpawnAction?.Invoke(netid, obj);
                }),
                _syncScale = netObjectBase.SyncScale,
                _syncTransform = netObjectBase.SyncTransform,
            };

            var spd = spawnData.InitialiseAdditionalSpawnData().Cast<GameObjectSpawnData>();
            spd.FromGameObject(netObjectBase.gameObject, netObjectBase.AreAnimationsNetworkControlled);
            spawnData.AdditionalSpawnData = spd;

            if (unified)
                SetupNetworkedObject((spawnData._creationMode == NetObjectCreationMode.Possess) ? netObjectBase.gameObject : null, spawnData);
        }

        void SetupNetworkedObject(GameObject gameObject, NetObjectSpawnData spawnData)
        {
            if (spawnData.NetID == default)
                spawnData.NetID = GlobalGameStateClient.Instance.NetObjectManager.GetNextNetID();

            if (spawnData.RmiIdentifier == default)
                spawnData._rmiIdentifier = RMIBehaviourManager.GetNextID();

            spawnData._postSpawnAction = null;

            LocalServerService.ServerManager.BroadcastMessage(new GameMessageServerSpawnObject()
            {
                _netObjectSpawnData = spawnData,
                _isAuth = false,
            });
        }

        void PlayNamedMethodOnObject(RMIIdentifier rmiID, int actionIndex, Il2CppReferenceArray<Il2CppSystem.Object> parameters)
        {
            string paramString = parameters == null ? "null" : string.Join(", ", parameters.Select(p => p?.ToString() ?? "null"));

            var target = RMIBehaviourManager.GetRMIBehaviour(rmiID);
            FLZ_Extensions.FGTLog(BepInEx.Logging.LogLevel.Debug, "PlayNamedMethodOnObject", $"rmiID = {rmiID}, actionIndex = {actionIndex}, foundObj = {target?.name}, parameters = [{paramString}]");

            LocalServerService.ServerManager.BroadcastMessage(new GameMessageServerEventGeneric()
            {
                Type = GameMessageServerEventGeneric.EventType.InvokeRMIOnObject,
                Data = new()
                {
                    MpgNetId = default,
                    FloatParam1 = default,
                    FloatParam2 = default,
                    IntParam1 = (int)rmiID._rmiIdentifier,
                    IntParam2 = actionIndex,
                    ObjectArray = parameters,
                    StrParam1 = default,
                    StrParam2 = default,
                }
            }, [LocalServerService.ServerManager.ServerNetID]);
        }

        void AwardTeamPoints(int teamId, int amount)
        {
            if (!ServerManager.CGM.GameRules.IsTeamGameMode)
                return;

            var currentScore = ServerManager.CGM._playerTeamManager.GetTeamScore(teamId);
            SetTeamScore(teamId, Mathf.Max(0, currentScore + amount));
        }

        void AwardAllTeamsPoints(int amount)
        {
            if (!ServerManager.CGM.GameRules.IsTeamGameMode)
                return;

            for (int i = 0; i < ServerManager.CGM._playerTeamManager._teamScores.Count; i++)
            {
                var currentScore = ServerManager.CGM._playerTeamManager.GetTeamScore(i);
                SetTeamScore(i, Mathf.Max(0, currentScore + amount));
            }
        }

        void SetTeamScore(int teamId, int newScore)
        {
            if (!ServerManager.CGM.GameRules.IsTeamGameMode)
                return;

            LocalServerService.ServerManager.BroadcastMessage(new GameMessageServerEventGeneric()
            {
                Type = GameMessageServerEventGeneric.EventType.TeamScoreUpdate,
                Data = new()
                {
                    MpgNetId = default,
                    IntParam2 = newScore,
                    FloatParam1 = default,
                    FloatParam2 = default,
                    IntParam1 = teamId,
                    ObjectArray = new(0),
                    StrParam1 = default,
                    StrParam2 = default,
                }
            });
        }

        void AwardPoints(MPGNetObject playerNetObj, int amount)
        {
            if (!ServerManager.CGM.GameRules.IsScoringGame)
                return;

            var currentScore = ServerManager.CGM._soloScoreManager.GetSoloScore(playerNetObj.NetID);
            var scoreAfter = Mathf.Min(ServerManager.CGM.GameRules.ScoreTarget, Mathf.Max(0, currentScore + amount));

            if (currentScore == scoreAfter)
                return;

            LocalServerService.ServerManager.BroadcastMessage(new GameMessageServerEventGeneric()
            {
                Type = GameMessageServerEventGeneric.EventType.SoloScoreUpdate,
                Data = new()
                {
                    MpgNetId = playerNetObj.NetID,
                    IntParam2 = scoreAfter,
                    FloatParam1 = default,
                    FloatParam2 = default,
                    IntParam1 = default,
                    ObjectArray = new(0),
                    StrParam1 = default,
                    StrParam2 = default,
                }
            });

            if (scoreAfter >= ServerManager.CGM.GameRules.ScoreTarget && !ServerManager.CGM.GetPlayerData(playerNetObj.NetID).completedLevel)
                MarkPlayerAsSuccessful(playerNetObj, true);
        }

        void SetScore(MPGNetObject playerNetObj, int newScore)
        {
            if (!ServerManager.CGM.GameRules.IsScoringGame)
                return;

            LocalServerService.ServerManager.BroadcastMessage(new GameMessageServerEventGeneric()
            {
                Type = GameMessageServerEventGeneric.EventType.SoloScoreUpdate,
                Data = new()
                {
                    MpgNetId = playerNetObj.NetID,
                    IntParam2 = newScore,
                    FloatParam1 = default,
                    FloatParam2 = default,
                    IntParam1 = default,
                    ObjectArray = new(0),
                    StrParam1 = default,
                    StrParam2 = default,
                }
            });
        }

        void TeleportNetObject(MPGNetObject netObject, Vector3 targetPosition, Quaternion targetRotation, SpawnReason spawnReason = SpawnReason.None)
        {
            if (netObject.IsFallGuy)
            {
                if (spawnReason == SpawnReason.Respawn)
                {
                    var tp = netObject.FGCharacterController.TeleportMotorFunction;
                    tp.RequestTeleport(targetPosition, targetRotation);
                }
                else
                    netObject.FGCharacterController.transform.SetPositionAndRotation(targetPosition, targetRotation);
            }
            else
                netObject.transform.SetPositionAndRotation(targetPosition, targetRotation);
        }

        void RespawnParticipant(FallGuysCharacterController fgcc)
        {
            var cgm = ServerManager.CGM;
            var playerData = cgm.GetPlayerData(fgcc.NetObject.NetID);

            var pos = ServerManager.CGM.GameRules.PickRespawnPosition(playerData.EntityID, playerData.SquadID, playerData.TeamID, playerData.VsGroupID, cgm.IsSquadShow);
            TeleportNetObject(fgcc.NetObject, pos.transform.position, pos.transform.rotation, SpawnReason.Respawn);
        }

        void IncreasePlayingTime(float amountInSeconds)
        {
            throw new NotImplementedException();
        }

        void DecreasePlayingTime(float amountInSeconds)
        {
            throw new NotImplementedException();
        }

        void SetPlayingTimeRemaining(float newTimeRemaining)
        {
            throw new NotImplementedException();
        }

        void SetJumbotronDisplay(JumbotronDisplayNetworkData displaydata)
        {
            LocalServerService.ServerManager.BroadcastMessage(new GameMessageServerEventGeneric()
            {
                Type = GameMessageServerEventGeneric.EventType.JumbotronDisplay,
                Data = new()
                {
                    MpgNetId = default,
                    IntParam2 = (int)displaydata.DisplayMode,
                    FloatParam1 = displaydata.CountdownSimulationTimeEnd,
                    FloatParam2 = displaydata.ExpectedCountdownDuration,
                    IntParam1 = displaydata.HashId,
                    ObjectArray = new(0),
                    StrParam1 = displaydata.DisplayText,
                    StrParam2 = default,
                }
            });
        }
    }
}
