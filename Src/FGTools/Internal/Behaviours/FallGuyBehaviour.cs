extern alias wle;
using System;
using System.Collections;
using System.Linq;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using DG.Tweening;
using Events;
using FG.Common;
using FG.Common.Audio;
using FG.Common.Character;
using FG.Common.Character.MotorSystem;
using FG.Common.Fraggle;
using FGClient;
using FGClient.UI;
using FGClient.UI.Core;
using FGTools.Config;
using FGTools.HarmonyPatches;
using FGTools.Services;
using FGTools.Services.Logic;
using FGTools.States;
using FGTools.States.Logic;
using Il2CppInterop.Runtime.Attributes;
using Levels.DoorDash;
using Levels.HexARing;
using Levels.HexSnake;
using Levels.Obstacles;
using Levels.Powerups;
using Levels.Progression;
using Levels.Rollout;
using Levels.ScoreZone;
using Levels.ScoreZone.FollowTheLeader;
using Levels.TimeAttack;
using Levels.TipToe;
using Mediatonic.Tools.Utils;
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
        public GameObject spawnpoint;
        public GameObject FallGuy;
        public FallGuysCharacterController FGCC;
        public MPGNetObject FGMPG;
        public GameplayState CurrentGPState;
        public int PlayerTeamId = -1;
        public bool IsInPseudoZone = false;

        bool delay;
        float timeRemaining = FGTServiceManager.Instance.GetService<RoundOptionsService>().ReturnLatestOptions().TimeLimitLength;
        string gamemodeType;
        public static int Rand = -1;


        public static FGRandom ThisRoundRandom()
        {
            if (Rand == -1)
                Rand = Random.Range(10, 102);

           MPGNetID id = new((uint)Rand);
           return FGRandom.Create(id, GlobalGameStateClient.Instance.GameStateView.RoundRandomSeed);
        }

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

            //if (StateManager.IsFGC)
            //{
            //    FraggleCommonManager.Instance.IsInLevelEditor = true;
            //    FraggleCommonManager.Instance.SetModeToExplore(new());
            //}

            PreFixObstacles();

            vfxplayer.InjectCameraScreenController(Resources.FindObjectsOfTypeAll<CameraScreenVFXController>().Last());

            PreloadPowAudio(Powerup.Value);

            if (SpeedrunMode.Value)
            {
                FMODTool.LoadBank("BNK_SFX_TimeAttack");
                float val = SPRespawnCD.Value;
                if (val > 0)
                    AttackOfTheTime.Display._timeAttackResetTime = val;
                else
                    AttackOfTheTime.Display._timeAttackResetTime = 0.1f;
            }

            FGTLog(LogLevel.Info, GetType(), $"Successful pre-init | Gamemode = {gamemodeType}");
        }

        void PreFixObstacles()
        {
            return;
            foreach (COMMON_PowerupPickup pow in Resources.FindObjectsOfTypeAll<COMMON_PowerupPickup>())
            {
                pow.SelectNextPowerup();
                pow.GeneratePowerup(true);
            }

            var rolloutManager = Resources.FindObjectsOfTypeAll<RolloutManager>().FirstOrDefault();

            if (rolloutManager != null && rolloutManager.gameObject.activeSelf)
            {
                int ringSchemas = rolloutManager._ringSegmentSchemas.Count;
                int ringLimit = Random.Range(2, ringSchemas);
                int addedRings = 0;
                //FGTLog(LogLevel.Info, base.GetType(), $"[ROLLOUTMANAGER] today our ring limit is {ringLimit}");

                for (int i = 0; i < ringSchemas; i++)
                {
                    if (addedRings < (RandomizeRings.Value ? ringLimit : ringSchemas))
                    //if (addedRings < ringSchemas)
                    {
                        int ringMax = Random.Range(0, rolloutManager._ringSegmentSchemas[i].PrefabPool.Count);
                        //FGTLog(LogLevel.Info, base.GetType(), $"[ROLLOUTMANAGER] today we instantiating ring with schema index {i} and prefab index {ringMax}");
                        rolloutManager.InstantiateRing(i, ringMax);
                        addedRings++;
                    }
                }
            }



            var flzone = Resources.FindObjectsOfTypeAll<FollowTheLeaderZone>().FirstOrDefault();
            if (flzone != null)
            {
                flzone.enabled = true;
                flzone.UpdateRadiusWithPlayerCount(FGTServiceManager.Instance.GetService<RoundOptionsService>().ReturnLatestOptions().PlayerCount);
                var flzone_trigger = Resources.FindObjectsOfTypeAll<VolumeZoneTrigger>().FirstOrDefault();
                if (flzone_trigger != null)
                {
                    flzone_trigger._volumeZone = flzone;
                }
                var flzone_manager = Resources.FindObjectsOfTypeAll<FollowTheLeaderManager>().FirstOrDefault();
                if (flzone_manager != null)
                {
                    flzone_manager.InitZones();
                }
            }

            var hexaringm = Resources.FindObjectsOfTypeAll<HexARingManager>().FirstOrDefault();
            if (hexaringm != null)
                hexaringm.ManagePlayingParticipantCount(FGTServiceManager.Instance.GetService<RoundOptionsService>().ReturnLatestOptions().PlayerCount);

            var hexshakem = Resources.FindObjectsOfTypeAll<HexSnakeManager>().FirstOrDefault();
            if (hexshakem != null)
                hexshakem.ManagePlayingParticipantCount(FGTServiceManager.Instance.GetService<RoundOptionsService>().ReturnLatestOptions().PlayerCount);

            var baskets = Resources.FindObjectsOfTypeAll<COMMON_SpawnBasket>();
            foreach (COMMON_SpawnBasket basket in baskets)
            {
                if (basket._spawnedItemGOs.Count > 0)
                {
                    int idx = 0;
                    foreach (GameObject target in basket._spawnedItemGOs)
                    {
                        //target.AddComponent<SpawnedObjectController>().Load(basket, idx, false);
                        idx++;
                    }
          
                }
                basket.enabled = true;
            }

            if (StateManager.IsFGC)
            {
                foreach (COMMON_SpawnBasket p in baskets)
                    p.SpawnItems_LevelEditor();

                //foreach (COMMON_ScoringBubble bubble in Resources.FindObjectsOfTypeAll<COMMON_ScoringBubble>())
                //{
                //    var target = bubble.gameObject.transform.GetChild(0).gameObject.GetComponent<COMMON_ScoringBubbleTrigger>();
                //    if (target != null)
                //    {
                //        target.gameObject.AddComponent<UGCBubble>();
                //        bubble._bubbleTrigger = target;
                //    }
                //}
            }

            foreach (COMMON_RoundProgressValueScaler a in Resources.FindObjectsOfTypeAll<COMMON_RoundProgressValueScaler>())
                a.enabled = true;
        }

        void PreloadPowAudio(SelectedPowerup power)
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
            CurrentGPState = StateManager.GetState<GameplayState>();
            ReDisplaySkipBtns(false);
        }
        
        //crap
        public void ReDisplaySkipBtns(bool ta)
        {
            foreach (var skipBtn in Resources.FindObjectsOfTypeAll<SkipRoundButton>().ToList().FindAll(x => x.gameObject.scene.name == "DontDestroyOnLoad"))
            {
                if (StateManager.IsInExplore && skipBtn.name.Contains("Skip"))
                    skipBtn.gameObject.SetActive(true);

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
                    spawnpoint.transform.SetPositionAndRotation(trans.position, trans.rotation);
                }
                else
                    FGCC.TeleportMotorFunction.RequestTeleport(spawnpoint.transform.position, spawnpoint.transform.rotation);

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
                FGTServiceManager.Instance.GetService<SpeedrunService>().SetSpawnPos(randPos.transform.position, randPos.transform.rotation);
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

        public void TryToElimPlayer()
        {
            if (StateManager.IsInGameplay)
            {
                if (CGM.GameRules.RoundEndCondition == RoundEndCondition.SuccessQuota || CGM.GameRules.RoundEndCondition == RoundEndCondition.TimeElapsed)
                    RespawnPlayer();

                else if (CGM.GameRules.RoundEndCondition == RoundEndCondition.EliminatedQuota)
                {
                    if (ElimLevel.Value != ElimType.None)
                        CurrentGPState.DoElim();
                    else
                        RespawnPlayer();
                }
            }
        }

     

        void CheckPlayerState()
        {
            return;

            //if (StateManager.CGM != null && StateManager.CGM.GameRules.HasScoreTarget == true)
            //{
            //    if (StateManager.CGM._soloScoreManager.GetSoloScore(FGMPG.NetID) >= StateManager.CGM.GameRules.ScoreTarget)
            //        CurrentGPState.DoQual();
            //}

            //if (FGCC != null && FGCC.CachedTransform.position.y < respawnPos)
            //{
            //    if (!FGTServiceManager.Instance.GetService<RoundLoaderService>().isXtremeRound)
            //        TryToElimPlayer();
            //    else
            //        CurrentGPState.DoElim();
            //}
        }

      
        

        private void Update()
        {
            FreeFlyController();
            CheckPlayerState();

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
                        spawnpoint.transform.position = FallGuy.transform.position;
                        FallGuy.GetComponent<FallGuysCharacterController>().CharacterEventSystem.RaiseEvent(FGEventFactory.GetVfxCheckpointEvent());
                        StartCoroutine(dumbDelay().WrapToIl2Cpp());
                    }

                    if (Input.GetKeyDown(ResetCheckpointHotkey.Value) && FallGuy != null && !RealHardMode.Value && !delay)
                    {
                        MultiplayerStartingPosition pos = CGM.GameRules.PickRespawnPosition(102, 0, PlayerTeamId, 0, false);
                        spawnpoint.transform.SetPositionAndRotation(pos.transform.position, pos.transform.rotation);
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

            //if (StateManager.CGM != null && StateManager.CGM.CurrentGameSession.CurrentSessionState == GameSession.SessionState.Countdown || StateManager.CGM.CurrentGameSession.CurrentSessionState == GameSession.SessionState.Playing)
            //    ServiceManager.Instance.GetService<RoundLoaderService>().RoundCamera.HandlePlayerCameraControls();
        }

        void OnTriggerEnter(Collider collision)
        {
            return;

            if (collision.gameObject.GetComponent<EndZoneVFXTrigger>() != null || collision.gameObject.GetComponent<COMMON_ObjectiveReachEndZone>() != null && QualLevel.Value != QualType.None)
                CurrentGPState.DoQual();
            
            if (collision.gameObject.GetComponent<COMMON_PlayerEliminationVolume>() != null && ElimLevel.Value != ElimType.None)
                CurrentGPState.DoElim();
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
            ServerGameStateActions.Instance.RespawnParticipant(FGCC);

            GameObject.Find(FallGuy.name + "/Character/GEO").SetActive(true);

            Broadcaster.Instance.Broadcast(new IntroCountdownEndedEvent());
            CGM._inGameUiManager.SwitchToState(InGameUiManager.InGameState.Playing);

            StateManager.HandleFGState(PlayerState.Active);

            if (!StateManager.IsFGC)
                StateManager.HandleFGTState(FGTStateManager.FGTState.GameActive);
            else
                StateManager.HandleFGTState(FGTStateManager.FGTState.FGCGameActive);

            if (SpeedrunMode.Value)
                StartCoroutine(FGTServiceManager.Instance.GetService<SpeedrunService>().NewRun().WrapToIl2Cpp());
            
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
