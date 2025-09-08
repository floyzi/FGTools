extern alias wle;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using DG.Tweening;
using Events;
using FG.Common;
using FG.Common.Audio;
using FG.Common.Character;
using FG.Common.Character.MotorSystem;
using FGClient;
using FGClient.Rendering.XRay;
using FGClient.UI;
using FGClient.UI.Core;
using FGTools.Services;
using FGTools.Services.Logic;
using FGTools.States;
using FGTools.States.Logic;
using Il2CppInterop.Runtime.Attributes;
using System.Collections;
using System.Linq;
using UnityEngine;
using static FGTools.Config.ConfigManager;
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
        public static FallGuyBehaviour _instance;
        public FreeCameraController fc;
        public GameObject controller;
        public GameObject FallGuy;
        public FallGuysCharacterController FGCC;
        public MPGNetObject FGMPG;
        public GameplayState CurrentGPState => StateManager.GetState<GameplayState>();
        public int PlayerTeamId = -1;
        public bool IsInPseudoZone = false;

        bool delay;
        float timeRemaining = FGTServiceManager.Instance.GetService<RoundOptionsService>().ReturnLatestOptions().TimeLimitLength;
        string gamemodeType;
        public static int Rand = -1;

        public void Awake()
        {
            if (_instance != null)
                Destroy(_instance);
            _instance = this;

            PreInit();
        }

        void PreInit()
        {
            if (Rand != -1)
                Rand = -1;

            FallGuy = gameObject;
            FGCC = gameObject.GetComponent<FallGuysCharacterController>();
            FGMPG = gameObject.GetComponent<MPGNetObject>();
            fc = gameObject.AddComponent<FreeCameraController>();
            gamemodeType = CGM._round.Archetype.Id.Split('_')[1];
            var vfxplayer = gameObject.GetComponent<FallGuyVFXController>();

            vfxplayer.InjectCameraScreenController(Resources.FindObjectsOfTypeAll<CameraScreenVFXController>().Last());

            PreloadPowAudio(Powerup.Value);

            FGTLog(LogLevel.Info, GetType(), $"Successful pre-init | Gamemode = {gamemodeType}");
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

            if (power == SelectedPowerup.None)
                return;

            var powerupFunc = motorAgent.GetMotorFunction<MotorFunctionPowerup>();
            var targetPowerup = "PowerupSO Rolling Ball";
            var GPIV = Resources.FindObjectsOfTypeAll<GameplayPowerupInventoryViewModel>().FirstOrDefault();

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

            if (PowerupInventory.Value && GPIV != null && GPIV._character == null)
            {
                GPIV.ResetInventory();
                GPIV._character = FGCC;
                GPIV.OnCharacterControllerInitialized();
                GPIV.gameObject.SetActive(true);
            }
        }

        public void Init()
        {
            SetPowerup(Powerup.Value);
            FGTLog(LogLevel.Info, GetType(), "Init");
        }

        public void OnGameplayBegin()
        {
            gameObject.GetComponent<Rigidbody>().isKinematic = false;
            ReDisplaySkipBtns(false);
        }
        
        //crap
        public void ReDisplaySkipBtns(bool ta)
        {
            foreach (var skipBtn in Resources.FindObjectsOfTypeAll<SkipRoundButton>().ToList().FindAll(x => x.gameObject.scene.name == "DontDestroyOnLoad"))
            {
                if (ta)
                {
                    if (CGM.GameRules.IsTimeAttackGameMode && skipBtn.name.Contains("Reset"))
                        skipBtn.gameObject.SetActive(true);
                }
            }
        }

        public void RespawnPlayer(bool ta = false)
        {
            if (FallGuy != null && StateManager.IsInGameplay && !RealHardMode.Value && !delay)
            {
                StartCoroutine(dumbDelay().WrapToIl2Cpp());
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

                FallGuy.GetComponent<FallGuysCharacterController>().ResetToDefaultState();
                FallGuy.GetComponent<Rigidbody>().DORestart();
                FallGuy.gameObject.GetComponent<Rigidbody>().velocity = new Vector3(0, 5, 0);
                FGTServiceManager.Instance.GetService<RoundLoaderService>().RoundCamera.OnRecenterAndSnapCameraNextFrameRequested();
            }
        }

        public void UpdateTeam(int forceTeam = -2, bool avoidtp = false)
        {
            if (CGM != null && CGM.GameRules.IsTeamGameMode)
            {
                if (forceTeam == -2)
                    PlayerTeamId = Random.Range(0, CGM.GameRules.NumTeamsWanted());
                MultiplayerStartingPosition randPos = CGM.GameRules.PickStartingPosition(PeakId, 0, PlayerTeamId, 0, false);
                FGCC.SetTeamID(PlayerTeamId);

                if (!avoidtp)
                    FGCC.TeleportMotorFunction.RequestTeleport(randPos.transform.position, randPos.transform.rotation);
                else
                    FallGuy.transform.SetPositionAndRotation(randPos.transform.position, randPos.transform.rotation);

                CGM.AssignPlayerToTeam(FGMPG.NetID, PlayerTeamId);
                var col = CustomisationManager.Instance.GetTeamNameColor(PlayerTeamId);
                FGCC.GetComponent<FallguyCustomisationHandler>().SetCostumeTeamColours(col);
                FGCC.GetComponent<FallguyCustomisationHandler>().UpdateBodyColours(col, col);
            }
        }

        public void FreeFlyController()
        {
            if (StateManager.FGCurrentState != PlayerState.FreeFly)
                return;

            FGCC.RigidBody.isKinematic = true;

            if (UIManager.Instance.GetScreen<InGameMenuViewModel>(ScreenStackType.PartyMenu) != null)
                return;

            if (Physics.Raycast(Camera.main.ScreenPointToRay(new Vector3(Screen.width / 2, Screen.height / 2, 0)), out RaycastHit hit))
                transform.rotation = Quaternion.Euler(0f, Quaternion.LookRotation(transform.position - hit.point, Vector3.up).eulerAngles.y, 0f);

            if (Input.GetKey(MoveFORWARD.Value))
                transform.Translate(Vector3.forward * FFMSpeedH.Value * Time.deltaTime);

            if (Input.GetKey(MoveBACKWARD.Value))
                transform.Translate(Vector3.back * FFMSpeedH.Value * Time.deltaTime);

            if (Input.GetKey(MoveLEFT.Value))
                transform.Translate(Vector3.left * FFMSpeedH.Value * Time.deltaTime);

            if (Input.GetKey(MoveRIGHT.Value))
                transform.Translate(Vector3.right * FFMSpeedH.Value * Time.deltaTime);

            if (Input.GetKey(MoveDOWN.Value))
                transform.Translate(Vector3.down * FFMSpeedV.Value * Time.deltaTime);

            if (Input.GetKey(MoveUP.Value))
                transform.Translate(Vector3.up * FFMSpeedV.Value * Time.deltaTime);

        }

        private void Update()
        {
            FreeFlyController();

            if (CGM != null && CGM.CurrentGameSession.CurrentSessionState == GameSession.SessionState.Playing)
            {
                if (timeRemaining > 0)
                    timeRemaining -= Time.deltaTime;

                if (!CGM.GameRules.IsTimeAttackGameMode && !delay)
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
                    if (Input.GetKeyDown(CheckpointHotkey.Value) && FallGuy != null && !RealHardMode.Value && !delay)
                    {
                        CurrentGPState.Spawnpoint.transform.position = FallGuy.transform.position;
                        FallGuy.GetComponent<FallGuysCharacterController>().CharacterEventSystem.RaiseEvent(FGEventFactory.GetVfxCheckpointEvent());
                        StartCoroutine(dumbDelay().WrapToIl2Cpp());
                    }

                    if (Input.GetKeyDown(ResetCheckpointHotkey.Value) && FallGuy != null && !RealHardMode.Value && !delay)
                    {
                        MultiplayerStartingPosition pos = CGM.GameRules.PickRespawnPosition(102, 0, PlayerTeamId, 0, false);
                        CurrentGPState.Spawnpoint.transform.SetPositionAndRotation(pos.transform.position, pos.transform.rotation);
                    }
                }
                if (Input.GetKeyDown(EnterFFM.Value) && FallGuy != null)
                {
                    if (StateManager.FGCurrentState != PlayerState.FreeFly)
                    {
                        StateManager.HandleFGState(PlayerState.FreeFly);
                        FallGuy.GetComponent<Rigidbody>().isKinematic = true;
                    }
                    else
                    {
                        StateManager.HandleFGState(PlayerState.Active);
                        FallGuy.GetComponent<Rigidbody>().isKinematic = false;
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


        [HideFromIl2Cpp]
        IEnumerator dumbDelay()
        {
            delay = true;
            yield return new WaitForSeconds(RCDelay.Value);
            delay = false;
        }

       
    }
}
