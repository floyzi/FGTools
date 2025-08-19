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
            if (wasOk)
            {
                if (StateManager.ExploreState == null)
                    __instance.LeaveMatch();
                else
                    StateManager.QuitExplore();
                FGTServiceManager.GetService<StatisticsService>().ProcessNewRound(StatisticsService.RoundResult.Leave);
            }
            return true;
        }

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
    }

    public class ThemePatches
    {
        [HarmonyPatch(typeof(MainMenuManager), "PlayMenuMusic")]
        [HarmonyPrefix]
        static bool PlayMenuMusic(MainMenuManager __instance, int playbackPosition)
        {
            MenuAudioProvider mainMenuCustomAudio = __instance.GetComponent<MenuAudioProvider>() ?? __instance.gameObject.AddComponent<MenuAudioProvider>();
            mainMenuCustomAudio?.PlayMusic(true);
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
            mainMenuCustomAudio?.StopMusic();
            return false;
        }
    }
}
