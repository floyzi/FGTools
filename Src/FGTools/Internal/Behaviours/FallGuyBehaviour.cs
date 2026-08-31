extern alias wle;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using DG.Tweening;
using Events;
using FG.Common;
using FG.Common.Audio;
using FG.Common.Character;
using FG.Common.Character.MotorSystem;
using FG.Common.Definition;
using FGClient;
using FGClient.Rendering.XRay;
using FGClient.UI;
using FGClient.UI.Core;
using FGTools.Services;
using FGTools.Services.Logic;
using FGTools.States;
using FGTools.States.Logic;
using FMODUnity;
using Il2CppInterop.Runtime.Attributes;
using System.Collections;
using System.Linq;
using UnityEngine;
using static FGTools.Config.Config;
using static FGTools.Internal.Extensions.FLZ_Extensions;
using static FGTools.Services.LocalizationService;
using static FGTools.Services.SpeedrunService;
using static FGTools.States.Logic.FGTStateManager;
using static FGTools.UI.ReadyPopups;
using Random = UnityEngine.Random;
using Vector3 = UnityEngine.Vector3;

namespace FGTools.Internal.Behaviours
{
    internal class FallGuyBehaviour : ToolsBehaviour
    {
        public const int PeakId = 102;
        internal static FallGuyBehaviour Instance;
        internal FreeCameraController FreeCamera;
        internal FallGuysCharacterController FGCC;
        internal GameplayState CurrentGPState => StateManager.GetState<GameplayState>();
        internal int PlayerTeamId = -1;
        internal bool IsInPseudoZone = false;

        float _nextRespawn;
        float _timeLeft = FGTServiceManager.Instance.GetService<RoundOptionsService>().ReturnLatestOptions().TimeLimitLength;
        string _gamemode;

        public void Awake()
        {
            if (Instance != null)
                Destroy(Instance);
            Instance = this;

            PreInit();
        }

        void PreInit()
        {
            FGCC = gameObject.GetComponent<FallGuysCharacterController>();
            FreeCamera = gameObject.AddComponent<FreeCameraController>();
            _gamemode = CGM._round.Archetype.Id.Split('_')[1];
            var vfxplayer = gameObject.GetComponent<FallGuyVFXController>();

            vfxplayer.InjectCameraScreenController(Resources.FindObjectsOfTypeAll<CameraScreenVFXController>().Last());

            PreloadPowAudio(Powerup.Value);
            FMODTool.LoadBank("BNK_SFX_TimeAttack");

            FGTLog(LogLevel.Info, GetType(), $"Successful pre-init | Gamemode = {_gamemode}");
        }

        static void PreloadPowAudio(SelectedPowerup power)
        {
            switch (power)
            {
                case SelectedPowerup.RollingBall:
                    FMODTool.LoadBank("BNK_SFX_PowerUp_RollingBall");
                    break;
                case SelectedPowerup.Invisibeans:
                    FMODTool.LoadBank("BNK_SFX_PowerUp_Invisibility");
                    break;
                case SelectedPowerup.ExplodingRhino:
                    FMODTool.LoadBank("BNK_SFX_PowerUp_ExplodingRhino");
                    break;
                case SelectedPowerup.RubberChicken:
                    FMODTool.LoadBank("BNK_SFX_PowerUp_RubberChicken");
                    break;
            }
        }

        public void SetPowerup(SelectedPowerup power)
        {
            var motorAgent = GetComponent<MotorAgent>();

            if (power == SelectedPowerup.None || power == SelectedPowerup.ExplodingRhino) //todo: rhino crashes the game whed used, needs fix!1
                return;

            var powerupFunc = motorAgent.GetMotorFunction<MotorFunctionPowerup>();
            var targetPowerup = "PowerupSO Rolling Ball";
            var inventory = Resources.FindObjectsOfTypeAll<GameplayPowerupInventoryViewModel>().FirstOrDefault();

            if (powerupFunc == null)
            {
                ErrorPopup("MotorFunctionPowerup is null");
                return;
            }

            switch (power)
            {
                case SelectedPowerup.RollingBall:
                    targetPowerup = "PowerupSO Rolling Ball";
                    break;
                case SelectedPowerup.Invisibeans:
                    targetPowerup = "PowerupSO Sneaky Bean";
                    break;
                case SelectedPowerup.ExplodingRhino:
                    targetPowerup = "PowerupSO Exploding Rhino";
                    break;
                case SelectedPowerup.RubberChicken:
                    targetPowerup = "PowerupSO Rubber Chicken";
                    break;
            }

            var pow = Resources.FindObjectsOfTypeAll<PowerupSO>().ToList().Find(x => x.ToString() == targetPowerup);
            if (pow == null)
            {
                ErrorPopup($"Unable to find powerup {targetPowerup}");
                return;
            }

            powerupFunc.EquippedPowerupData.Init(pow, PowerupAmount.Value, PowerupLength.Value, InfPowerups.Value);

            if (PowerupInventory.Value && inventory != null && inventory._character == null)
            {
                inventory.ResetInventory();
                inventory._character = FGCC;
                inventory.OnCharacterControllerInitialized();
                inventory.gameObject.SetActive(true);
                inventory.UpdateUI(powerupFunc.EquippedPowerupData, pow);
                PreloadPowAudio(Powerup.Value);
            }
        }

        public void Init()
        {
            SetPowerup(Powerup.Value);
            FGTLog(LogLevel.Info, GetType(), "Init");
        }

        public void OnGameplayBegin()
        {
            FMODTool.CreateFMODEvent("SFX_TimeAttack_Snapshot_TimeStop", out var _);
            gameObject.GetComponent<Rigidbody>().isKinematic = false;
        }

        public void RespawnPlayer(bool ta = false)
        {
            if (FGCC != null && StateManager.IsInGameplay && !RealHardMode.Value && _nextRespawn < Time.time)
            {
                StateManager.FGCurrentState = PlayerState.Active;

                if (ta)
                {
                    FGTController.CheckpointManager._netIDToCheckpointMap.Clear();
                    var trans = CGM.GameRules.PickRespawnPosition(PeakId, 0, PlayerTeamId, 0, false).gameObject.transform;
                    FGCC.TeleportMotorFunction.RequestTeleport(trans.position, trans.rotation);
                    CurrentGPState.Spawnpoint.transform.SetPositionAndRotation(trans.position, trans.rotation);
                }
                else
                    FGCC.TeleportMotorFunction.RequestTeleport(CurrentGPState.Spawnpoint.transform.position, CurrentGPState.Spawnpoint.transform.rotation);

                FGCC.ResetToDefaultState();
                FGCC.RigidBody.DORestart();
                FGCC.RigidBody.velocity = new Vector3(0, 5, 0);
                CGM.CameraDirector.OnRecenterAndSnapCameraNextFrameRequested();

                _nextRespawn = Time.time + 0.5f;
            }
        }

        public void UpdateTeam(int forceTeam = -2, bool avoidtp = false)
        {
            if (CGM != null && CGM.GameRules.IsTeamGameMode)
            {
                if (forceTeam == -2) PlayerTeamId = Random.Range(0, CGM.GameRules.NumTeamsWanted());

                var randPos = CGM.GameRules.PickStartingPosition(PeakId, 0, PlayerTeamId, 0, false);
                FGCC.SetTeamID(PlayerTeamId);

                if (!avoidtp)
                    FGCC.TeleportMotorFunction.RequestTeleport(randPos.transform.position, randPos.transform.rotation);
                else
                    FGCC.transform.SetPositionAndRotation(randPos.transform.position, randPos.transform.rotation);

                CGM.AssignPlayerToTeam(FGCC.NetObject.NetID, PlayerTeamId);
                var col = CustomisationManager.Instance.GetTeamNameColor(PlayerTeamId);

                FGCC.CustomisationHandler.SetCostumeTeamColours(col);
                FGCC.CustomisationHandler.UpdateBodyColours(col, col);
            }
        }

        string InitialCollideWithTag = null;
        public void FreeFlyController()
        {
            if (StateManager.FGCurrentState != PlayerState.FreeFly)
                return;

            FGCC.RigidBody.isKinematic = true;

            if (UIManager.Instance.GetScreen<InGameMenuViewModel>(ScreenStackType.PartyMenu) != null)
                return;

            var move = Vector3.zero;

            var lookDir = Camera.main.transform.forward;
            lookDir.y = 0f;

            CGM.CameraDirector._closeCameraAvoidance._collideOnlyWithTag = "[REDACTED]";

            if (lookDir != Vector3.zero)
                transform.rotation = Quaternion.LookRotation(lookDir);

            if (Input.GetKey(MoveFORWARD.Value))
                move += transform.forward * FFMSpeedH.Value;

            if (Input.GetKey(MoveBACKWARD.Value))
                move -= transform.forward * FFMSpeedH.Value;

            if (Input.GetKey(MoveLEFT.Value))
                move -= transform.right * FFMSpeedH.Value;

            if (Input.GetKey(MoveRIGHT.Value))
                move += transform.right * FFMSpeedH.Value;

            if (Input.GetKey(MoveDOWN.Value))
                move -= transform.up * FFMSpeedV.Value;

            if (Input.GetKey(MoveUP.Value))
                move += transform.up * FFMSpeedV.Value;

            FGCC.RigidBody.position += move * Time.deltaTime;

        }

        private void Update()
        {
            if (FGCC == null || CGM == null || CGM.CurrentGameSession == null) return;

            FreeFlyController();
            
            if (CGM.CurrentGameSession.CurrentSessionState == GameSession.SessionState.Playing)
            {
                if (_timeLeft > 0) _timeLeft -= Time.deltaTime;

                if (!CGM.GameRules.IsTimeAttackGameMode && _nextRespawn < Time.time)
                {
                    if (Input.GetKeyDown(RespawnHotkey.Value))
                    {
                        if (SpeedrunMode.Value && FGTServiceManager.Instance.GetService<SpeedrunService>().SpeedrunState != RunState.TempDisabled)
                            return;
                        else
                            RespawnPlayer();
                    }
                }
                if (!CGM.GameRules.IsTimeAttackGameMode && StateManager.IsInGameplay)
                {
                    if (Input.GetKeyDown(CheckpointHotkey.Value) && !RealHardMode.Value && _nextRespawn < Time.time)
                    {
                        CurrentGPState.Spawnpoint.transform.position = FGCC.transform.position;
                        FGCC.CharacterEventSystem.RaiseEvent(FGEventFactory.GetVfxCheckpointEvent());
                    }

                    if (Input.GetKeyDown(ResetCheckpointHotkey.Value) && !RealHardMode.Value && _nextRespawn < Time.time)
                    {
                        var pos = CGM.GameRules.PickRespawnPosition(102, 0, PlayerTeamId, 0, false);
                        CurrentGPState.Spawnpoint.transform.SetPositionAndRotation(pos.transform.position, pos.transform.rotation);
                    }
                }
                if (Input.GetKeyDown(EnterFFM.Value))
                {
                    if (StateManager.FGCurrentState != PlayerState.FreeFly)
                    {
                        InitialCollideWithTag ??= CGM.CameraDirector._closeCameraAvoidance._collideOnlyWithTag;
                        StateManager.HandleFGState(PlayerState.FreeFly);
                        FGCC.RigidBody.isKinematic = true;
                    }
                    else
                    {
                        StateManager.HandleFGState(PlayerState.Active);
                        FGCC.RigidBody.isKinematic = false;
                        CGM.CameraDirector._closeCameraAvoidance._collideOnlyWithTag = InitialCollideWithTag;
                    }
                    FGTServiceManager.Instance.GetService<RoundLoaderService>().RoundCamera.OnRecenterAndSnapCameraNextFrameRequested();
                }
            }
        }

        public void ReturnToStart()
        {
            CurrentGPState.winComplete = false;
            CurrentGPState.winResultsPending = false;
            CurrentGPState.elimComplete = false;
            CurrentGPState.qualComplete = false;

            if (!FGTServiceManager.Instance.GetService<EventService>().ReturnBoolEventValue("OldSp"))
            {
                CreateNotification(LocalizedStr("msg_tip"), LocalizedStr("msg_old_spc_possible"), FGT_Info_Color);
                FGTServiceManager.Instance.GetService<EventService>().SetEventValue("OldSp", true);
            }

            try { AudioMixing.Instance.ResetAllSnapshotParams(); } catch { }
            CGM.FinishPreparationPhase();
            CGM.GameRules.RemovePlayerFromSuccessfulList(FGCC.NetObject.NetID.m_NetworkID);

            CGM.SetClockPaused(false);
            CGM._physicsSimulator.SetRunningPhysicsAutomatically(true);
            FGBehaviour.FGCC.RigidBody.isKinematic = false;
            FGBehaviour.FGCC.CustomisationHandler.HandleCostumeVisibility(true);
            FGBehaviour.FGCC.CustomisationHandler.HandleFallGuyVisibility(true);
            XRayUtils.AddXRayControllerForCharacter(FGBehaviour.FGCC);

            ServerGameStateActions.Instance.RespawnParticipant(FGCC);

            Broadcaster.Instance.Broadcast(new IntroCountdownEndedEvent());
            CGM._inGameUiManager.SwitchToState(InGameUiManager.InGameState.Playing);

            StateManager.HandleFGState(PlayerState.Active);

            if (!StateManager.IsFGC)
                StateManager.HandleFGTState(FGTStateManager.ToolsState.GameActive);
            else
                StateManager.HandleFGTState(FGTStateManager.ToolsState.FGCGameActive);

            if (SpeedrunMode.Value)
                FGTServiceManager.Instance.GetService<SpeedrunService>().NewRun();
            
            CGM.CountdownEnds();
        }
    }
}
