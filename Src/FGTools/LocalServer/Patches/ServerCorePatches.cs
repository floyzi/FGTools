extern alias wle;
using FG.Common;
using FG.Common.Character;
using FGClient;
using FGTools.Services;
using FGTools.Services.Logic;
using FGTools.States.Logic;
using HarmonyLib;
using Mediatonic.Networking;
using UnityEngine;
namespace FGTools.LocalServer.Patches
{
    /// <summary>
    /// General patches for local server
    /// </summary>
    public class ServerCorePatches : FGTBase
    {
        [HarmonyPatch(typeof(MotorFunctionBeingGrabbedStateInactive), nameof(MotorFunctionBeingGrabbedStateInactive.Begin)), HarmonyPostfix]
        static void Begin(MotorFunctionBeingGrabbedStateInactive __instance, int prevState)
        {
            __instance._motorFunctionBeingGrabbed = __instance.GetMotorFunction<MotorFunctionBeingGrabbed>();
        }

        [HarmonyPatch(typeof(MotorFunctionRollingBallStateBounce), nameof(MotorFunctionRollingBallStateBounce.Begin)), HarmonyPostfix]
        static void Begin(MotorFunctionRollingBallStateBounce __instance, int prevState)
        {
            __instance.BounceFeedback();
        }

        [HarmonyPatch(typeof(StateGameLoading), nameof(StateGameLoading.OnPlayerSpawned)), HarmonyPrefix]
        static bool OnPlayerSpawned(StateGameLoading __instance, MPGNetObject pNetObject, uint playerID, FG_NetworkID playerNetworkID, string accountId, string platformId, string playerName, string playerGeneratedName, uint squadId, int teamId, string partyId, int vsGroupId, bool tailEnabled, CustomisationSelections customisationSelections)
        {
            ServerManager.OnPlayerSpawned?.Invoke(pNetObject, playerID, playerNetworkID, accountId, platformId, playerName, playerGeneratedName, squadId, teamId, partyId, vsGroupId, tailEnabled, customisationSelections);
            return true;
        }

        [HarmonyPatch(typeof(FG_UnityInternetNetworkManager), nameof(FG_UnityInternetNetworkManager.ServerHandleMessageReceived)), HarmonyPrefix]
        static bool ServerHandleMessageReceived(FG_UnityInternetNetworkManager __instance, NetworkMessage msg)
        {
            ServerManager.OnServerReceivedMessage?.Invoke(msg);
            return true;
        }

        [HarmonyPatch(typeof(ClientGameManager), nameof(ClientGameManager.SetupPlayerUpdateManager)), HarmonyPrefix]
        static bool SetupPlayerUpdateManager(ClientGameManager __instance)
        {
            if (LocalServerService.IsServerInOperation && __instance._playerUpdateManager == null)
            {
                __instance._playerUpdateManager = new GameObject("_ServerPlayerUpdateManager").AddComponent<ServerPlayerUpdateManager>();
                return false;
            }
            else
                return true;
        }

        [HarmonyPatch(typeof(ClientPlayerManager), nameof(ClientPlayerManager.OnPlayerSpawned)), HarmonyPrefix]
        static bool OnPlayerSpawned(ClientPlayerManager __instance, MPGNetObject pNetObject)
        {
            NetworkPlayerDataClient clientPlayerData = __instance.GetClientPlayerDataForNetId(pNetObject.NetID);

            if (clientPlayerData != null && clientPlayerData.isLocalPlayer)
            {
                var playerInput = pNetObject.GetComponent<FallGuysCharacterControllerInput>();

                if (playerInput != null)
                {
                    playerInput.SetPlayerIndex(0);
                    clientPlayerData.inputIndex = 0;
                    playerInput.AcceptInput = false;
                }
            }
            return false;
        }

        [HarmonyPatch(typeof(MPGNetMotorAgentState), nameof(MPGNetMotorAgentState.SendMessage)), HarmonyPrefix]
        static bool SendMessage(MPGNetMotorAgentState __instance, bool bypassNetworkLOD = false)
        {
            FG_NetworkManager.DeliveryType deliveryTypeToOwningClient = FG_NetworkManager.DeliveryType.None;
            FG_NetworkManager.DeliveryType deliveryTypeToRemoteClient = FG_NetworkManager.DeliveryType.Unreliable;

            if (__instance._messageImportance >= SnapshotChange.Important || __instance._appliedTaskImportance >= SnapshotChange.Important)
            {
                deliveryTypeToOwningClient = FG_NetworkManager.DeliveryType.Reliable;
            }
            __instance._appliedTaskImportance = SnapshotChange.None;
            FG_NetworkID[] exclusionArray = null;
            int amountExcluded = 0;

            __instance._messageSender.Invoke(__instance._nextMotorAgentMessage, __instance._netObject.NetID, deliveryTypeToOwningClient, deliveryTypeToRemoteClient, exclusionArray, amountExcluded);
            return false;
        }

        [HarmonyPatch(typeof(LeaveMatchPopupManager), nameof(LeaveMatchPopupManager.LeaveMatch)), HarmonyPrefix]
        static bool LeaveMatch(LeaveMatchPopupManager __instance)
        {
            if (LocalServerService.IsServerInOperation)
                FGTServiceManager.Instance.GetService<LocalServerService>().ShutdownSerer(null);
            return true;
        }

        [HarmonyPatch(typeof(FallGuysCharacterController), nameof(FallGuysCharacterController.OnManagedUpdate_Server)), HarmonyPostfix]
        static void OnManagedUpdate_Server(FallGuysCharacterController __instance, float simulationTime, float fixedSimulationTime, float deltaTime)
        {
            __instance._fxController.OnManagedUpdate(deltaTime, __instance.Lod_ReadOnly);

            if (!__instance.IsLocalPlayer)
                __instance.OnManagedUpdate_Remote(deltaTime);

            LocalServerService.ServerManager?.ParseTasks(__instance.MotorAgent.MotorTasks.MotorTasks, __instance);
        }

        [HarmonyPatch(typeof(FallGuysCharacterController), nameof(FallGuysCharacterController.OnManagedFixedUpdate_Server)), HarmonyPostfix]
        static void OnManagedFixedUpdate_Server(FallGuysCharacterController __instance, bool physicsSimulationDisabled, Vector3 gravityVector)
        {
            if (!__instance.IsLocalPlayer)
                __instance.OnManagedFixedUpdate_Remote(Time.frameCount, Time.renderedFrameCount, false, ServerManager.CGM.CurrentGameSession.SimulationFixedTime, gravityVector);
        }

        [HarmonyPatch(typeof(FallGuysCharacterController), nameof(FallGuysCharacterController.OnManagedLateFixedUpdate_LocalOrServer)), HarmonyPostfix]
        static void OnManagedLateUpdate_Server(FallGuysCharacterController __instance)
        {
            if (!__instance.IsLocalPlayer)
                __instance.OnManagedLateFixedUpdate_Remote();
        }

        [HarmonyPatch(typeof(StateDisconnectingFromServer), nameof(StateDisconnectingFromServer.Initialise)), HarmonyPrefix]
        static bool GameMessageReceived(StateDisconnectingFromServer __instance)
        {
            if (LocalServerService.IsServerInOperation)
                FGTServiceManager.GetService<LocalServerService>().ShutdownSerer(null);
            return true;
        }
    }
}
