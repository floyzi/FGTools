extern alias wle;
using FG.Common;
using FGClient;
using FGClient.ShowSelector;
using FGClient.UI.Core;
using FGTools.Internal.Behaviours;
using FGTools.Internal.Extensions;
using FGTools.Services;
using FGTools.States.Logic;
using FGTools.UI;
using FMODUnity;
using HarmonyLib;
using System;
using System.IO;
using static FGTools.Internal.Behaviours.FallGuyBehaviour;
using static FGTools.Internal.Extensions.FLZ_Extensions;
using static FGTools.Services.LocalizationService;
using static FGTools.States.Logic.FGTStateManager;
using static FGTools.UI.FGToolsUI.NewGUI;

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

        [HarmonyPatch(typeof(CameraDirector), nameof(CameraDirector.UpdateAudioListenerTransform))]
        [HarmonyPrefix]
        static bool UpdateAudioListenerTransform(CameraDirector __instance)
        {
            if (StateManager.FGCurrentState != PlayerState.FreeCam)
                return true;

            var listener = AudioManager.HasInstance ? AudioManager.Instance.Listener : null;
            if (listener == null)
                return true;

            listener.transform.SetPositionAndRotation(_instance.fc.CAM.transform.position, _instance.fc.CAM.transform.rotation);
            return false;
        }

        [HarmonyPatch(typeof(BootSplashScreenViewModel), nameof(BootSplashScreenViewModel.Awake)), HarmonyPostfix]
        static void Awake(BootSplashScreenViewModel __instance)
        {
            if (File.Exists(Launcher.Splash))
                __instance._slides.Add(GetSpriteFromFile(Launcher.Splash, 1920, 1080));
        }

        [HarmonyPatch(typeof(LoadingScreenViewModel), nameof(LoadingScreenViewModel.Update)), HarmonyPostfix]
        static void UpdateDisplay(LoadingScreenViewModel __instance)
        {
            if (File.Exists(Launcher.LoadingScreen) && __instance._loadingScreenImage.sprite.name != Path.GetFileNameWithoutExtension(Launcher.LoadingScreen))
                __instance._loadingScreenImage.sprite = GetSpriteFromFile(Launcher.LoadingScreen, 1920, 1080);

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

            var gui = FGToolsUI.NewGUI.Instance;
            gui.ToggleUI(true);

            if (StateManager.InternalState.LoaderUIToggle)
                return false;

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
            if (MenuAudioProvider.instance == null)
                return true;

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


            MenuAudioProvider.instance.needToFadeOut = true;
            MenuAudioProvider.instance.celebrationPreview = true;

            return false;
        }

        [HarmonyPatch(typeof(CelebrationPreviewFullscreenPopupViewModel), nameof(CelebrationPreviewFullscreenPopupViewModel.CloseScreen)), HarmonyPrefix]
        static bool CloseScreen(CelebrationPreviewFullscreenPopupViewModel __instance)
        {
            if (MenuAudioProvider.instance == null)
                return true;

            AudioManager.PlayOneShot(AudioManager.EventMasterData.GenericCancel, default);
            __instance.HideScreen();

            MenuAudioProvider.instance.celebrationPreview = false;
            MenuAudioProvider.instance.needToFadeIn = true;

            return false;
        }
    }

}
