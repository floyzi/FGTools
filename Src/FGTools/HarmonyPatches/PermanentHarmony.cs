extern alias wle;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using Events;
using FG.Common;
using FGClient;
using FGClient.ShowSelector;
using FGClient.UI.Core;
using FGTools.Internal;
using FGTools.Internal.Behaviours;
using FGTools.Services;
using FGTools.States.Logic;
using FGTools.UI;
using FMODUnity;
using HarmonyLib;
using Il2CppSystem.Collections.Generic;
using Levels.Obstacles;
using LiveOps.Challenges;
using LiveOps.TimeAttack;
using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UniverseLib.UI;
using static FGTools.Config.ConfigManager;
using static FGTools.Internal.Behaviours.FallGuyBehaviour;
using static FGTools.Internal.Extensions.FLZ_Extensions;
using static FGTools.Services.LocalizationService;
using static FGTools.States.Logic.FGTStateManager;
using static FGTools.UI.FGToolsUI.NewGUI;
using static UnityEngine.Object;

namespace FGTools.HarmonyPatches
{
    public class GlobalPatches : FGTBase
    {
        [HarmonyPatch(typeof(FraggleLevelDataRepository), nameof(FraggleLevelDataRepository.OnFailure)), HarmonyPrefix]
        static bool OnFailure(FraggleLevelDataRepository __instance, string error, Action<Exception> onRequestFailedAction, LevelLookupKey levelLookup = null)
        {
            try
            {
                StateManager.RoundLoadingAllowed = true;
                UIManager.Instance.HideScreen<LoadingSpinnerScreenViewModel>();
                FGTServiceManager.GetService<RoundLoaderService>().preloadedDTO = null;
                FGToolsUI.NewGUI.Instance.levelInfo.text = $"<b>{LocalizedStr("failed_desc_new")}</b>\n\n{LocalizedStr("gui_fgc_not_found")}\n\n({error} | {levelLookup.ShareCode})";
            }
            catch { }
            return false;
        }

        [HarmonyPatch(typeof(EOSAntiCheatService), nameof(EOSAntiCheatService.Init)), HarmonyPrefix]
        static bool EACPatch(EOSAntiCheatService __instance)
        {
            __instance.SetAllowOnlinePlay(false, LocalizedStr("fgt_eac_error"), false);
            return false;
        }

        [HarmonyPatch(typeof(AFKManager), nameof(AFKManager.Init)), HarmonyPrefix]
        static bool Start(AFKManager __instance)
        {
            __instance.enabled = false;
            return false;
        }
#if !LAN_MULTIPLAYER

        [HarmonyPatch(typeof(ClientPlayerManager), nameof(ClientPlayerManager.OnPlayerSpawned)), HarmonyPrefix]
        static bool Blank()
        {
            return false;
        }
#endif
        [HarmonyPatch(typeof(CameraDirector), "UpdateAudioListenerTransform")]
        [HarmonyPrefix]
        static bool UpdateAudioListenerTransform(CameraDirector __instance)
        {
            StudioListener listener = AudioManager.HasInstance ? AudioManager.Instance.Listener : null;
            if (listener != null)
            {
                var currentFocusTarget = __instance.CurrentFocus;
                var audioListenerTransform = listener.transform;
                var mainCamTransform = __instance.MainNativeCam.transform;

                if (StateManager.FGCurrentState != PlayerState.FreeCam)
                    audioListenerTransform.SetPositionAndRotation((currentFocusTarget != null) ? currentFocusTarget.transform.position : mainCamTransform.position, mainCamTransform.rotation);
                else
                    audioListenerTransform.SetPositionAndRotation(_instance.fc.CAM.transform.position, _instance.fc.CAM.transform.rotation);
            }
            return false;
        }

#if !LAN_MULTIPLAYER
        [HarmonyPatch(typeof(ClientGameManager), "OnLocalPlayerTimeAttackResetRequest")]
        [HarmonyPrefix]
        static bool OnLocalPlayerTimeAttackResetRequest(ClientGameManager __instance, OnLocalPlayerTimeAttackResetRequest evt)
        {
            var b = Resources.FindObjectsOfTypeAll<TimeAttackLapDisplay>().FirstOrDefault();
            if (StateManager.CGM.GameRules.IsTimeAttackGameMode)
            {
                FallGuyBehaviour._instance.RespawnPlayer(true);
                FGTController.TimeAttackManager.GetPlayerStats(102).RestartCurrentLap();
                b._currentLocalTimeAttackLapState = TimeAttackLapState.NotStarted;
                //b.ShouldShowTimeAttackResetInput = false;
                Resources.FindObjectsOfTypeAll<TimeAttackItemManager>().FirstOrDefault().TryResetPlayerItems(FGBehaviour.FGMPG);
            }
            else if (SpeedrunMode.Value)
            {
                FGTBehaviour.StartCoroutine(ServiceManagerFGT.GetService<SpeedrunService>().NewRun().WrapToIl2Cpp());
                b._currentLocalTimeAttackLapState = TimeAttackLapState.NotStarted;
                //b.ShouldShowTimeAttackResetInput = false;
            }
            return false;
        }

        [HarmonyPatch(typeof(CheckpointManager), "OnCheckpointReached")]
        [HarmonyPrefix]
        static bool CM_OnCheckpointReached(CheckpointManager __instance, CheckpointZone cpz, MPGNetObject mpgno)
        {
            try
            {
                bool isLapMode = StateManager.CGM.GameRules.ScoreDisplayMode == ScoreDisplayModes.Lap;
                bool reached = __instance.HandleCheckpointReached(cpz, mpgno);
                bool ta = StateManager.CGM.GameRules.IsTimeAttackGameMode;
                if (reached)
                {
                    __instance._netIDToCheckpointMap[mpgno.NetID] = cpz.UniqueId;

                    if (cpz.ShowVisualsWhenReached)
                    {
                        if (!DisableCheckpoints.Value)
                        {
                            __instance.TriggerVFX(cpz, mpgno);
                            cpz.GetNextSpawnPositionAndRotation(out Vector3 targetPosition, out Quaternion targetRotation);
                            if (Physics.Raycast(targetPosition + FGBehaviour.FGCC.Data.TeleportRaycastOffset, Vector3.down, out RaycastHit hit, float.PositiveInfinity, FGBehaviour.FGCC.Data.groundCheckLayers.layerMask, QueryTriggerInteraction.Ignore))
                                targetPosition = hit.point + FGBehaviour.FGCC.Data.TeleportPositionOffset;
                            FallGuyBehaviour._instance.spawnpoint.transform.SetPositionAndRotation(targetPosition, targetRotation);
                            //FGTLog(LogLevel.Info, base.GetType(), $"[CHECKPOINTMANAGER] Set checkpoint {cpz.name} for {mpgno.name} ({mpgno.NetID})");
                            if (SpeedrunMode.Value && !isLapMode)
                                ServiceManagerFGT.GetService<SpeedrunService>().SaveRunTimer(SpeedrunService.SpeedrunSaveType.Checkpt);
                        }
                        if (ta)
                        {
                            TimeAttackLapDisplay display = Resources.FindObjectsOfTypeAll<TimeAttackLapDisplay>().FirstOrDefault();
                            TimeAttackManager currManager = Resources.FindObjectsOfTypeAll<TimeAttackManager>().FirstOrDefault();
                            TimeAttackLapData data = currManager.GetPlayerStats(102).GetCurrentLap;
                            Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<float> ElapsedArray = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<float>(data.ElapsedTime.ToArray());
                            Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<float> Deductions = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<float>(data.Deductions.ToArray());
                            display.HandleTimeAttackLocalSplitUpdated(new TimeAttackLocalSplitUpdated
                            {
                                Split = ElapsedArray.Length - 1,
                                Lap = data.CurrentLapIndex,
                                CurrentSplit = ElapsedArray.Sum()
                            });
                            currManager.GetPlayerStats(102).RegisterTimeUpdate(TimeAttackLapState.InProgress, TimeAttackUpdateType.Split, data.CurrentLapIndex, ElapsedArray, Deductions, data.PauseTime, true);

                        }
                    }
                }
                if (isLapMode)
                {
                    FallGuysCharacterController FGCC = mpgno.FGCharacterController;
                    FGCC.OnNetColliderTriggered();
                    bool lapDone = false;
                    if (__instance._netIDToCheckpointMap.ContainsKey(mpgno.NetID))
                        lapDone = __instance._netIDToCheckpointMap[mpgno.NetID] == __instance._checkpointZones[__instance._checkpointZones.Length - 1].UniqueId;
                    if (lapDone)
                    {
                        if (SpeedrunMode.Value && isLapMode)
                            ServiceManagerFGT.GetService<SpeedrunService>().SaveRunTimer(SpeedrunService.SpeedrunSaveType.Lap);
                        __instance._netIDToCheckpointMap[mpgno.NetID] = 0U;
                        Broadcaster.Instance.Broadcast(new LocalPlayerLapCompleteEvent(FGCC));
                        StateManager.CGM._soloScoreManager.AwardSoloPoints(mpgno.NetID, 1);
                        AudioManager.Instance.PlayOneShot(AudioManager.EventMasterData.CheckpointLap, default);
                        if (StateManager.CGM._soloScoreManager.GetSoloScore(mpgno.NetID) >= StateManager.CGM.GameRules.ScoreTarget)
                            StateManager.ReturnState<GameplayState>().DoQual();
                    }

                }
                if (OneTimeCheckpoint.Value)
                    return reached;
                else

                    return false;
            }
            catch
            {
                return false;
            }
        }
#endif

        [HarmonyPatch(typeof(LoadingScreenViewModel), nameof(LoadingScreenViewModel.UpdateDisplay)), HarmonyPostfix]
        static void UpdateDisplay(LoadingScreenViewModel __instance, string messageId, bool hideMessageText, bool hideSpinner, string progressBarTitleId, bool hideProgressBar, bool hideGameplayLoop, float progressBarPercentage, bool hideDownloadBytesProgress, long bytesDownloaded, long bytesTotal)
        {
            if (File.Exists(Plugin.LoadingSplash))
                __instance._loadingScreenImage.sprite = SetSpriteFromFile(Plugin.LoadingSplash, 1920, 1080);

            __instance.ShowGameplayLoop = false;
        }

        [HarmonyPatch(typeof(TitleScreenViewModel), nameof(TitleScreenViewModel.ExitTitleScreen)), HarmonyPrefix]
        static bool ExitSplashscreen(TitleScreenViewModel __instance)
        {
            __instance.ClearViewModel();
            __instance.CheckToShowLinkedProgressPopup();
            StateManager.HandleFGTState(FGTStateManager.FGTState.CMSParsed);
            FGTServiceManager.GetService<OnlineCheckService>().Run();
            return false;
        }

        [HarmonyPatch(typeof(MainMenuShowSelectorPlayButtonViewModel), nameof(MainMenuShowSelectorPlayButtonViewModel.Play)), HarmonyPrefix]
        static bool Play(MainMenuShowSelectorPlayButtonViewModel __instance)
        {
            AudioManager.PlayOneShot(AudioManager.EventMasterData.MainMenuPlay);

            if (StateManager.InternalState.LoaderUIToggle)
                return false;

            StateManager.InternalState.LoaderUIToggle = true;
            var gui = FGToolsUI.NewGUI.Instance;
            gui.UIRoot.gameObject.SetActive(true);
            UniversalUI.SetUIActive(UniverseGUID, true);

            gui.GoToTab(Tab.RoundLoader, SubLevel.Default);
            gui.GoToTab(Tab.RoundLoader_Main, SubLevel.RoundLoader);
            return false;
        }

        [HarmonyPatch(typeof(LeaveMatchPopupManager), nameof(LeaveMatchPopupManager.OnClose)), HarmonyPrefix]
        static bool OnClose(LeaveMatchPopupManager __instance, bool wasOk)
        {
            AudioManager.PlayOneShot(AudioManager.EventMasterData.GenericCancel, default(Vector3));
            if (wasOk)
            {
                LeaveLobbySilentlyIfNecessaryEvent evt = new LeaveLobbySilentlyIfNecessaryEvent("PartyStateManager_Core_HandlePartyEntered", LeaveLobbyReason.EnteredParty);
                Broadcaster.Instance.Broadcast<LeaveLobbySilentlyIfNecessaryEvent>(evt);
                if (CGM != null && StateManager.FGTCurrentState == FGTStateManager.FGTState.RoundIntro)
                {
                    foreach (var fmodevt in CGM._ambienceInstances)
                    {
                        fmodevt?.Stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
                    }

                    FMODTool.UnloadAllLoadedBanks(FMODTool.UnloadParam.Default);
                    CGM.Shutdown();
                }

                if (StateManager.ExploreState == null)
                    __instance.LeaveMatch();
                else
                    StateManager.QuitExplore();
                FGTServiceManager.GetService<StatisticsService>().ProcessNewRound(StatisticsService.RoundResult.Leave);
                PartyStateManager.Instance.HidePartyMenu();
            }
            else
            {
                __instance.CloseScreen();
                if (PartyStateManager.Instance.IsInPartyWithOthers() && InGameOptionsMenuManager.Instance.IsScreenActive && ShowsManager.Instance.SelectedGameMode == ShowsManager.GameMode.Public)
                {
                    PartyStateManager.Instance.ShowPartyMenu();
                }
            }
            return false;
        }

#if !LAN_MULTIPLAYER
        [HarmonyPatch(typeof(RoundLoader), nameof(RoundLoader.CleanupLoadingScreens)), HarmonyPrefix]
        static bool CleanupLoadingScreens(RoundLoader __instance)
        {
            return false;
        }

        [HarmonyPatch(typeof(RoundLoader), nameof(RoundLoader.FinalizeShowLoadingGameScreenForUGC)), HarmonyPrefix]
        static bool FinalizeShowLoadingGameScreenForUGC(RoundLoader __instance, LevelInfoDto levelInfo, Round round)
        {
            var Metadata = new ScreenMetaData()
            {
                Transition = ScreenTransitionType.FadeInAndOut,
                ScreenStack = ScreenStackType.LoadingScreen,
                RewiredMap = 17,
                Data = new UGCLoadingGameScreenDto()
                {
                    Round = round,
                    levelInfoDto = ((levelInfo != null) ? levelInfo : LevelInfoDto.CreatePlaceholderData())
                },
                OnOpenedAction = new Action(() =>
                {
                    MainMenuManager mainMenuManager = Service<MainMenuManager>.Get();
                    if (mainMenuManager != null)
                        mainMenuManager.RemoveMainMenuBuilder();
                })
            };

            if (!StateManager.IsPlayingExploreFGC)
                UIManager.Instance.ShowScreen<LoadingUGCGameScreenViewModel>(Metadata);
            else
                UIManager.Instance.ShowScreen<LoadingUPGameScreenViewModel>(Metadata);

            return false;
        }
#endif

        [HarmonyPatch(typeof(GenericCelebrationPreviewViewModel), nameof(GenericCelebrationPreviewViewModel.ShowFullScreenPopup)), HarmonyPrefix]
        static bool ShowFullScreenPopup()
        {
            AudioManager.PlayOneShot(AudioManager.EventMasterData.GenericPopUpAppears, default);
            UIManager.Instance.ShowScreen<CelebrationPreviewFullscreenPopupViewModel>(new ScreenMetaData
            {
                ScreenStack = ScreenStackType.Popup,
                RewiredMap = 5,
                UseScrim = true,
                Transition = ScreenTransitionType.FadeInAndOut,
                Data = new CelebrationPreviewFullscreenPopupData
                {
                    MuteAnimation = false
                }
            });

            if (MenuAudioProvider.instance != null)
            {
                MenuAudioProvider.instance.needToFadeOut = true;
                MenuAudioProvider.instance.celebrationPreview = true;
            }
            return false;
        }

        [HarmonyPatch(typeof(CelebrationPreviewFullscreenPopupViewModel), nameof(CelebrationPreviewFullscreenPopupViewModel.CloseScreen)), HarmonyPrefix]
        static bool CloseScreen(CelebrationPreviewFullscreenPopupViewModel __instance)
        {
            AudioManager.PlayOneShot(AudioManager.EventMasterData.GenericCancel, default);
            __instance.HideScreen();
            if (MenuAudioProvider.instance != null)
            {
                MenuAudioProvider.instance.celebrationPreview = false;
                MenuAudioProvider.instance.needToFadeIn = true;
            }
            return false;
        }

#if !LAN_MULTIPLAYER
        [HarmonyPatch(typeof(ClientGameManager), nameof(ClientGameManager.OnLocalPlayerResetToCheckpointRequest)), HarmonyPrefix]
        static bool OnLocalPlayerResetToCheckpointRequest(ClientGameManager __instance, LocalPlayerResetToCheckpointRequest evt)
        {
            Resources.FindObjectsOfTypeAll<InGameMenuViewModel>().FirstOrDefault().HideScreen();
            FGBehaviour.RespawnPlayer();
            return false;
        }

        [HarmonyPatch(typeof(ClientGameManager), nameof(ClientGameManager.OnLocalPlayerSkipRound)), HarmonyPrefix]
        static bool OnLocalPlayerSkipRound(ClientGameManager __instance, CurrentPlayerSkipRound evt)
        {
            StateManager.HandleFGState(PlayerState.Finish);
            StateManager.ReturnState<GameplayState>().EndGameplay();
            QualifiedScreenViewModel.Show("skipped", new Action(act));
            StateManager.ExploreState.SkippedRoundsCount++;
            AudioManager.PlayGameplayEndAudio(true);
            static void act() => StateManager.ExploreState.RequestNewRound();
            return false;
        }

        [HarmonyPatch(typeof(InGameMenuViewModel), "ToggleOpen")]
        [HarmonyPrefix]
        static bool ToggleOpen(InGameMenuViewModel __instance, bool isInGameMenuOpen, bool playSound = true)
        {
            if (playSound)
                InGameMenuViewModel.PlaySFX(isInGameMenuOpen);
            if (__instance._settingsSubMenuViewModel != null)
                __instance._settingsSubMenuViewModel.HideScreen();
            if (!isInGameMenuOpen)
                __instance.HideScreen();

            if (StateManager.FGTCurrentState == FGTStateEnum.GamePaused)
                StateManager.HandleFGTState(FGTStateEnum.GameActive);

            if (TimePause.Value)
            {
                if (isInGameMenuOpen)
                    StateManager.HandleFGTState(FGTStateEnum.GamePaused);
                else
                    StateManager.HandleFGTState(FGTStateEnum.GameActive);
            }
            return false;
        }
#endif
    }

    public class FGCGameplay : FGTBase
    {
        //[HarmonyPatch(typeof(wle.LevelEditorTriggerZoneActiveBase), nameof(wle.LevelEditorTriggerZoneActiveBase.OnTriggerEnter)), HarmonyPrefix]
        //static bool AwardPoints(wle.LevelEditorTriggerZoneActiveBase __instance, Collider collider)
        //{
        //    if (collider.gameObject.GetComponent<MPGNetObject>() != null && collider.gameObject.GetComponent<MPGNetObject>().IsFallGuy && !FGBehaviour.IsInPseudoZone)
        //    {
        //        if (__instance._scoringType == ScoringType.OnEnter)
        //        {
        //            __instance._levelEditorTriggerScoreFeedback.EnableBlinkAnimation();
        //            __instance._levelEditorTriggerScoreFeedback.SetPointsScored(__instance._pointsScored);
        //            __instance._levelEditorTriggerScoreFeedback.EnableScoreFeedback(FallGuyBehaviour.PeakId, __instance._disableVisual);
        //            StateManager.CGM._soloScoreManager.AwardSoloPoints(FGBehaviour.FGMPG.NetID, __instance._pointsScored);
        //        }
        //        FGBehaviour.PlayerEnterPseudoZone();
        //    }
        //    return false;
        //}

        //[HarmonyPatch(typeof(wle.LevelEditorTriggerZoneActiveBase), nameof(wle.LevelEditorTriggerZoneActiveBase.OnTriggerExit)), HarmonyPrefix]
        //static bool OnTriggerExit(wle.LevelEditorTriggerZoneActiveBase __instance, Collider collider)
        //{
        //    if (collider.gameObject.GetComponent<MPGNetObject>() != null && collider.gameObject.GetComponent<MPGNetObject>().IsFallGuy)
        //        FGBehaviour.PlayerLeavePseudoZone();
        //    return false;
        //}

        //[HarmonyPatch(typeof(wle.LevelEditorTriggerZoneActiveBase), nameof(wle.LevelEditorTriggerZoneActiveBase.AwardPoints)), HarmonyPrefix]
        //static bool AwardPoints(wle.LevelEditorTriggerZoneActiveBase __instance)
        //{
        //    __instance._levelEditorTriggerScoreFeedback.EnableBlinkAnimation();
        //    __instance._levelEditorTriggerScoreFeedback.SetPointsScored(__instance._pointsScored);
        //    __instance._levelEditorTriggerScoreFeedback.EnableScoreFeedback(FallGuyBehaviour.PeakId, __instance._disableVisual);
        //    StateManager.CGM._soloScoreManager.AwardSoloPoints(FGBehaviour.FGMPG.NetID, __instance._pointsScored);
        //    return false;
        //}


        [HarmonyPatch(typeof(wle.LevelEditorCommonFlipper), nameof(wle.LevelEditorCommonFlipper.ServerStartFlip)), HarmonyPrefix]
        static bool ServerStartFlip(wle.LevelEditorCommonFlipper __instance)
        {
            CardinalDirection flipDirection = __instance.SelectNextFlipDirection();
            __instance.ServerFlipRequested((int)flipDirection);
            return false;
        }

        [HarmonyPatch(typeof(wle.LevelEditorCommonFlipper), nameof(wle.LevelEditorCommonFlipper.HandleState)), HarmonyPrefix]
        static bool HandleState(wle.LevelEditorCommonFlipper __instance)
        {
            if (!__instance.IsObjectCurrentlyActive() && __instance._currentFlipState == wle.LevelEditorCommonFlipper.FlipState.Ready)
                return false;
            switch (__instance._currentFlipState)
            {
                case wle.LevelEditorCommonFlipper.FlipState.Ready:
                    if (__instance._activationCondition == wle.LevelEditorCommonFlipper.ActivationCondition.Timed)
                    {
                        float delay = __instance.timeBetweenFlips;
                        if (__instance._randomCooldown && !__instance._hasRandomizedFlip)
                        {
                            __instance.timeBetweenFlips = __instance._fgRandom.Range(__instance.timeBetweenFlipsMin, __instance.timeBetweenFlipsMax);
                            __instance._hasRandomizedFlip = true;
                        }
                        if (__instance._inStartDelay)
                        {
                            delay = __instance._elapsedDelay;
                        }
                        __instance.WaitForDelayThenPerformAction(delay, new Action(__instance.ServerStartFlip));

                    }
                    break;
                case wle.LevelEditorCommonFlipper.FlipState.Flipping:
                    __instance.MoveToRotation(__instance._returnLocalRotation, __instance._extendedLocalRotation, __instance._flipStateDuration, wle.LevelEditorCommonFlipper.FlipState.Extended, new Action(__instance.HandleExtendComplete));
                    break;
                case wle.LevelEditorCommonFlipper.FlipState.Extended:
                    __instance.WaitForDelayThenChangeState(__instance._extendedStateDuration, wle.LevelEditorCommonFlipper.FlipState.Returning);
                    break;
                case wle.LevelEditorCommonFlipper.FlipState.Returning:
                    __instance.MoveToRotation(__instance._extendedLocalRotation, __instance._returnLocalRotation, __instance._returningStateDuration, wle.LevelEditorCommonFlipper.FlipState.Ready, new Action(__instance.RefreshFlipDirection));
                    break;
            }

            return false;
        }

        [HarmonyPatch(typeof(COMMON_SpawnBasket), nameof(COMMON_SpawnBasket.SpawnItems_LevelEditor)), HarmonyPrefix]
        static bool SpawnItems_LevelEditor(COMMON_SpawnBasket __instance)
        {
            for (int i = __instance._spawnedItemGOs.Count; i < __instance._itemCount; i++)
            {
                if (i >= __instance.SpawnPointTransforms.Length)
                    break;

                Transform itemParent = __instance.SpawnPointTransforms[i];
                GameObject carryObjectGO = Instantiate(__instance._prefabToSpawn, itemParent);
                __instance.OnItemSpawned(new((uint)UnityEngine.Random.Range(10000, 999999)), carryObjectGO);
            }
            __instance.UpdateAllItemRespawners();
            return false;
        }

        [HarmonyPatch(typeof(COMMON_SpawnBasket), nameof(COMMON_SpawnBasket.Awake)), HarmonyPrefix]
        static bool Awake(COMMON_SpawnBasket __instance)
        {
            if (__instance._transformToMonitor == null)
                __instance._transformToMonitor = __instance.transform;

            if (__instance._spawnPointsSets.Count > 0)
                __instance._currentSpawnPointsSet = __instance._spawnPointsSets[0];

            if (__instance._prefabToSpawn != null)
                __instance._currentPrefabMPGNetObject = __instance._prefabToSpawn.GetComponent<MPGNetObject>();

            if (COMMON_SpawnBasket._carryObjectPool == null)
                COMMON_SpawnBasket._carryObjectPool = new Dictionary<CarryType, Stack<GameObject>>();

            __instance._destroyTriggerY = -37.5f;
            __instance.UpdateAllItemRespawners();
            return false;
        }
    }

    public class ThemePatches
    {

        [HarmonyPatch(typeof(MainMenuManager), "PlayMenuMusic")]
        [HarmonyPrefix]
        static bool PlayMenuMusic(MainMenuManager __instance, int playbackPosition)
        {
            MenuAudioProvider mainMenuCustomAudio = __instance.GetComponent<MenuAudioProvider>();
            if (mainMenuCustomAudio == null)
                mainMenuCustomAudio = __instance.gameObject.AddComponent<MenuAudioProvider>();

            if (mainMenuCustomAudio != null)
                mainMenuCustomAudio.PlayMusic(true);
            return false;
        }

        [HarmonyPatch(typeof(MainMenuManager), "PauseMusic")]
        [HarmonyPrefix]
        static bool PauseMusic(MainMenuManager __instance, bool immediate)
        {
            return false;
        }

        [HarmonyPatch(typeof(MainMenuManager), "StopMusic")]
        [HarmonyPrefix]
        static bool StopMusic(MainMenuManager __instance, bool immediate)
        {
            MenuAudioProvider mainMenuCustomAudio = __instance.GetComponent<MenuAudioProvider>();
            if (mainMenuCustomAudio != null)
                mainMenuCustomAudio.StopMusic();
            return false;
        }
    }
}
