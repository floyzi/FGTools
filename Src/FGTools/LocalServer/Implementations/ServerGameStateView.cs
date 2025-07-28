using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BepInEx.Logging;
using FG.Common;
using FGClient;
using Il2CppInterop.Runtime.Attributes;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;
using static FGTools.Internal.Extensions.FLZ_Extensions;

namespace FGTools.LocalServer.Implementations
{
    [Il2CppImplements(typeof(IGameStateView))]
    public class ServerGameStateView : Il2CppSystem.Object
    {
        public ServerGameStateView() : base(ClassInjector.DerivedConstructorPointer<ServerGameStateView>())
        {
            ClassInjector.DerivedConstructorBody(this);
        }
        public MPGNetObjectManager GetNetObjectManager => GlobalGameStateClient.Instance.GameStateView.GetNetObjectManager;
        public bool IsNetworkedGame => GlobalGameStateClient.Instance.GameStateView.IsNetworkedGame;
        public bool IsGameServer => true;
        public bool HasAuthority => GlobalGameStateClient.Instance.GameStateView.HasAuthority;
        public bool IsLocalGameClient => GlobalGameStateClient.Instance.GameStateView.IsLocalGameClient;
        public bool HasGameClient => GlobalGameStateClient.Instance.GameStateView.HasGameClient;
        public bool IsAutomatedClient => GlobalGameStateClient.Instance.GameStateView.IsAutomatedClient;
        public bool IsUsingInstancedAutomatedClients => GlobalGameStateClient.Instance.GameStateView.IsUsingInstancedAutomatedClients;
        public bool IsComboServer => GlobalGameStateClient.Instance.GameStateView.IsComboServer;
        public bool IsStandaloneServer => GlobalGameStateClient.Instance.GameStateView.IsStandaloneServer;
        public bool IsGameCountingDown => GlobalGameStateClient.Instance.GameStateView.IsGameCountingDown;
        public bool IsGamePlaying => GlobalGameStateClient.Instance.GameStateView.IsGamePlaying;
        public bool IsGameEnded => GlobalGameStateClient.Instance.GameStateView.IsGameEnded;
        public bool IsGameAlive => GlobalGameStateClient.Instance.GameStateView.IsGameAlive;
        public bool IsGameLevelLoaded => GlobalGameStateClient.Instance.GameStateView.IsGameLevelLoaded;
        public string GameLevelName => GlobalGameStateClient.Instance.GameStateView.GameLevelName;
        public float CountdownTimeRemaining => GlobalGameStateClient.Instance.GameStateView.CountdownTimeRemaining;
        public float GameplayTimeRemaining => GlobalGameStateClient.Instance.GameStateView.GameplayTimeRemaining;
        public bool ShouldDisplayTimeRemainingNow => GlobalGameStateClient.Instance.GameStateView.ShouldDisplayTimeRemainingNow;
        public float GameplayTimeElapsed => GlobalGameStateClient.Instance.GameStateView.GameplayTimeElapsed;
        public float RoundProportionElapsed => GlobalGameStateClient.Instance.GameStateView.RoundProportionElapsed;
        public float DiscreteRoundProportionElapsed => GlobalGameStateClient.Instance.GameStateView.DiscreteRoundProportionElapsed;
        public float SimulationTime => GlobalGameStateClient.Instance.GameStateView.SimulationTime;
        public float SimulationDeltaTime => GlobalGameStateClient.Instance.GameStateView.SimulationDeltaTime;
        public float SimulationFixedTime => GlobalGameStateClient.Instance.GameStateView.SimulationFixedTime;
        public float SimulationFixedDeltaTime => GlobalGameStateClient.Instance.GameStateView.SimulationFixedDeltaTime;
        public int FixedFrameCount => GlobalGameStateClient.Instance.GameStateView.FixedFrameCount;
        public float CurrentEstimatedLatency => GlobalGameStateClient.Instance.GameStateView.CurrentEstimatedLatency;
        public int CurrentPacketLost => GlobalGameStateClient.Instance.GameStateView.CurrentPacketLost;
        public int CurrentPacketSent => GlobalGameStateClient.Instance.GameStateView.CurrentPacketSent;
        public int RoundRandomSeed => GlobalGameStateClient.Instance.GameStateView.RoundRandomSeed;
        public bool IsSquadShow => GlobalGameStateClient.Instance.GameStateView.IsSquadShow;
        public int SquadSize => GlobalGameStateClient.Instance.GameStateView.SquadSize;
        public bool CaptureMode => GlobalGameStateClient.Instance.GameStateView.CaptureMode;
        public uint InitialRoundPlayerCount => GlobalGameStateClient.Instance.GameStateView.InitialRoundPlayerCount;
        public bool IsPlayer(GameObject potentialPlayer, out FallGuysCharacterController controller) => GlobalGameStateClient.Instance.GameStateView.IsPlayer(potentialPlayer, out controller);
        public bool IsPlayerCollider(Collider potentialCollider) => GlobalGameStateClient.Instance.GameStateView.IsPlayerCollider(potentialCollider);
        public bool IsPlayerCollider(Collider potentialCollider, out FallGuysCharacterController fgcc) => GlobalGameStateClient.Instance.GameStateView.IsPlayerCollider(potentialCollider, out fgcc);
        public bool IsForceReceptivePlayerCollider(Collider potentialCollider, out FallGuysCharacterController fgcc) => GlobalGameStateClient.Instance.GameStateView.IsForceReceptivePlayerCollider(potentialCollider, out fgcc);
        public bool IsPlayerOrRagdollCollider(Collider potentialPlayerCollider) => GlobalGameStateClient.Instance.GameStateView.IsPlayerOrRagdollCollider(potentialPlayerCollider);
        public bool IsPlayerOrRagdollCollider(Collider potentialCollider, out FallGuysCharacterController fgcc) => GlobalGameStateClient.Instance.GameStateView.IsPlayerOrRagdollCollider(potentialCollider, out fgcc);
        public bool IsForceReceptivePlayerOrRagdollCollider(Collider potentialCollider, out FallGuysCharacterController fgcc) => GlobalGameStateClient.Instance.GameStateView.IsForceReceptivePlayerOrRagdollCollider(potentialCollider, out fgcc);
        public MPGNetObject GetNetObjectByID(MPGNetID mpgNetID) => GlobalGameStateClient.Instance.GameStateView.GetNetObjectByID(mpgNetID);
        public GameObject GetNetObjectByJointTargetDescription(JointTargetDescription jtd) => GlobalGameStateClient.Instance.GameStateView.GetNetObjectByJointTargetDescription(jtd);
        public void ClearGeometryCache() => GlobalGameStateClient.Instance.GameStateView.ClearGeometryCache();
        public void RegisterGeometryHashID(uint HashID, GameObject Object) => GlobalGameStateClient.Instance.GameStateView.RegisterGeometryHashID(HashID, Object);
        public bool CanCompleteObjectives(MPGNetObject otherNetObject) => GlobalGameStateClient.Instance.GameStateView.CanCompleteObjectives(otherNetObject);
        public string PlayerKeyForPlayerId(uint playerID) => GlobalGameStateClient.Instance.GameStateView.PlayerKeyForPlayerId(playerID);
        public string PlayerPlatformForPlayerId(uint playerID) => GlobalGameStateClient.Instance.GameStateView.PlayerPlatformForPlayerId(playerID);
        public string PlayerKeyForNetId(MPGNetID netID) => GlobalGameStateClient.Instance.GameStateView.PlayerKeyForNetId(netID);
        public string PlayerPlatformForNetId(MPGNetID netID) => GlobalGameStateClient.Instance.GameStateView.PlayerPlatformForNetId(netID);
        public int CurrentTeamScore(int teamId) => GlobalGameStateClient.Instance.GameStateView.CurrentTeamScore(teamId);
        public int TeamSize(int teamId) => GlobalGameStateClient.Instance.GameStateView.TeamSize(teamId);
        public int CurrentTeamSize(int teamId) => GlobalGameStateClient.Instance.GameStateView.CurrentTeamSize(teamId);
        public float GetCurrentEstimatedLatencyFor(MPGNetObject netObject) => GlobalGameStateClient.Instance.GameStateView.GetCurrentEstimatedLatencyFor(netObject);
        public void ForceRespawnStuckPlayerOnTheServer(FallGuysCharacterController fgcc) => GlobalGameStateClient.Instance.GameStateView.ForceRespawnStuckPlayerOnTheServer(fgcc);
    }
}
