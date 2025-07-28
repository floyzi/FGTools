extern alias wle;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using DG.Tweening;
using Events;
using FG.Common.CMS;
using FGClient;
using FGClient.CatapultServices;
using FGClient.UI;
using FGDebug;
using FGTools.Content;
using FGTools.Internal;
using FGTools.Internal.Behaviours;
using FGTools.Services;
using FGTools.States.Logic;
using FGTools.UI;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using NAudio.Wave;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;
using UniverseLib.UI;
using static FGTools.Config.ConfigManager;
using static FGTools.Internal.Extensions.FLZ_Extensions;
using static FGTools.Services.LocalizationService;
using static FGTools.UI.ReadyPopups;

namespace FGTools.States
{
    public class MenuState : FGTState
    {
        public bool menuComplete = false;
        public MainMenuManager menuManager;
        AssetBundleRequest theIntro;
        static bool pastLeaveOfflineAct = false;
        LoadingSpinnerScreenViewModel spinnerloading;
        bool missingFilesAct = false;
        bool verAlert = false;
        private bool splashChanged = false;
        public CatapultServicesManager catapultServicesManager;
        private bool offlineLoginPart1 = false;
        private bool offlineLoginPart2 = false;
        bool canPerfomOnMenu = true;
        public override void OnStateExit()
        {
            Commands.OnMenuEnter -= OnMenuEnter;
            Resources.FindObjectsOfTypeAll<MainMenuManager>().FirstOrDefault()?.StopMusic();
        }

        //bool CheckIfInMenu()
        //{
        //    if (menuManager == null)
        //        return false;
        //    else
        //        return menuManager.IsOnMainMenu && (menuManager._mainMenuBuilder.MainMenuViewModel.IsViewOnTop || menuManager._mainMenuBuilder.LevelEditorScreen.IsViewOnTop);
        //}


        void MakeTooltips()
        {
            Dictionary<string, object> formats = new() 
            {
                { "fgt_tooltip_01", SkipIntroHotkey.Value },
                { "fgt_tooltip_03", ToggleUIHotkey.Value }
            };

            var currData = CMSLoader.Instance.CMSData.ToolTipsData["singleton"];

            if (currData != null)
            {
                var tooltips = LocalizedStrings.Where(pair => pair.Key.StartsWith("fgt_tooltip_")).Select(pair =>
                {
                    var actualVal = pair.Value;

                    if (formats.ContainsKey(pair.Key))
                        actualVal = string.Format(actualVal, formats[pair.Key]);

                    return new ToolTip
                    {
                        _platform = ToolTip.TipPlatform.All,
                        _localisedStringtext = new LocalisedString { Text = actualVal }
                    };
                }).ToArray();

                FGTLog(LogLevel.Info, base.GetType(), $"Got {tooltips.Length} possible tooltips");

                currData._tips = new Il2CppReferenceArray<ToolTip>(tooltips);
            }
            else
                FGTLog(LogLevel.Info, base.GetType(), "fuck.");
        }

        bool introInProgress = false;
        IEnumerator GamingIntro(Action after)
        {
            if (!FGTTargetSettings.GamingIntro)
            {
                after();
                yield break;
            }

            if (!introInProgress)
            {
                if (theIntro.asset != null)
                {
                    introInProgress = true;
                    FGTLog(LogLevel.Info, base.GetType(), "Gaming intro time!!");
                    var loopEvent = new WaveOutEvent();
                    var outp = GameObject.Instantiate(theIntro.asset);

                    yield return outp;

                    if (MenuAudioProvider.instance == null)
                        menuManager.PauseMusic(true);
                    else
                    {
                        menuManager.StopMusic();
                        MenuAudioProvider.instance.StopMusic();
                        FMODTool.UnloadBank("BNK_Music_MainMenu");
                    }
                    var loopWaveProvider = new WaveFileReader(Plugin.IntroMusic);
                    var loopVolumeWaveProvider = new VolumeWaveProvider16(loopWaveProvider) { Volume = GlobalGameStateClient.Instance.PlayerProfile.AudioSettings.MusicVolume * GlobalGameStateClient.Instance.PlayerProfile.AudioSettings.MasterVolume };
                    loopEvent.Init(loopVolumeWaveProvider);
                    loopEvent.Play();

                    var Out = GameObject.Find("CoolGamingIntro(Clone)");
                    var intro = Out.transform.GetChild(0).gameObject.GetComponent<VideoPlayer>();

                    yield return new WaitForSeconds((float)intro.length);

                    FGTLog(LogLevel.Info, base.GetType(), "Played intro.");
                    introInProgress = false;
                    Out.transform.GetChild(1).transform.GetComponent<RawImage>().DOFade(0, 1.2f);
                    GameObject.Destroy(Out.gameObject, 1.7f);
                    after.Invoke();
                    if (MenuAudioProvider.instance == null)
                        menuManager.ResumeMusic();
                    yield break;
                }
                else
                    after.Invoke();
            }
        }


        void MenuEvent()
        {
            StateManager.ExploreState = null;
            StateManager.ShowState = null;
            StateManager.RoundLoadingAllowed = true;

            FGTServiceManager.GetService<RoundLoaderService>().SetupCMSRoundList();
            FGTServiceManager.GetService<StatisticsService>().ValidateStats(GlobalGameStateClient.Instance.PlayerProfile.PlatformAccountName);
            FGTServiceManager.GetService<CosmeticsService>().Load();

            MakeTooltips();

            if (UseCustomFonts.Value && StateManager.InternalState.TargetFont != null)
            {
                foreach (Text txt in Resources.FindObjectsOfTypeAll<Text>())
                {
                    try
                    {
                        if (txt.transform.IsChildOf(FGToolsUI.NewGUI.Instance.ContentRoot.transform) || txt.transform.IsChildOf(SearchPanel.instance.ContentRoot.transform))
                            txt.font = StateManager.InternalState.TargetFont;
                    }
                    catch { }
                }
            }
            if (!StateManager.PiratedGame)
            {
                var evtS = FGTServiceManager.GetService<EventService>();
                if (/*offlineMode.Value*/ !StateManager.PiratedGame)
                {
                    var targetVer = Plugin.BuildInfo.Version;
                    var eventVer = evtS.ReturnStringEventValue("MenuEntranceVersion");

                    if (eventVer != null && targetVer != eventVer)
                    {
                        MenuPopup();
                        evtS.SetEventValue("MenuEntranceVersion", targetVer.ToString());
                    }
                }

                //else if (offlineMode.Value)
                //{
                //    foreach (FallguyCustomisationHandler handler in Resources.FindObjectsOfTypeAll<FallguyCustomisationHandler>())
                //    {
                //        handler.UpdateSmoothness(0f, 0f);
                //        handler.UpdateMetallic(0f, 0f);
                //    }
                //    MenuPopup();
                //    if (pastLeaveOfflineAct && menuManager._titleScreen != null)
                //        menuManager._titleScreen.HideScreen();
                //    pastLeaveOfflineAct = false;
                //}

                StateManager.HandleFGTState(FGTStateManager.FGTState.Menu);
                canPerfomOnMenu = true;
            }
        }

        public override void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
           
        }

        public override void OnStateSet()
        {
            theIntro = Plugin.FGToolsBundle.LoadAssetAsync<GameObject>("CoolGamingIntro");
            Commands.OnMenuEnter += OnMenuEnter;
        }

        void OnMenuEnter()
        {
            FGTLog(LogLevel.Info, base.GetType(), "OnMenuEnter()");

            FGTServiceManager.GetService<MenuThemeService>().OnMenuEnterEvent();
            FMODTool.UnloadAllLoadedBanks(FMODTool.UnloadParam.Default);

            if (LocalServerService.IsServerInOperation)
                FGTServiceManager.GetService<LocalServerService>().ShutdownSerer(null);

            Action finishLogin = new(() => 
            {
                if (!StateManager.FirstTimeLogin)
                {
                    if (!StateManager.PiratedGame)
                    {
                        Plugin.UniverseUIBase ??= UniversalUI.RegisterUI(UniverseGUID, null);
#if DEV
                        Broadcaster.Instance.Broadcast<GlobalDebug.DebugToggleFPSCounter>(new());
#endif
                        if (FGToolsUI.NewGUI.Instance == null)
                            StateManager.InternalState.ToolsUI = new (Plugin.UniverseUIBase);
                    }
                    
                    StateManager.FirstTimeLogin = true;
                }
                StateManager.CanUseHotkeys = true;
                MenuEvent();
            });

            var targetVer = Plugin.BuildInfo.Version;
            var evtVer = FGTServiceManager.GetService<EventService>().ReturnStringEventValue("MenuEntranceVersion");

            if (evtVer != null && targetVer != evtVer && !introInProgress)
                CoroutineRunner.Instance.StartCoroutine(GamingIntro(finishLogin).WrapToIl2Cpp());
            else
                finishLogin();
        }

        public override void UpdateState()
        {
            string activeScene = SceneManager.GetActiveScene().name;
            if (activeScene == "Boot")
                return;

           
            if (!TargetFGVersions.Contains(Application.version) && !verAlert && activeScene == "MainMenu" && FGTTargetSettings.VersionWarning)
            {
                DoModal(LocalizedStr("version_warning_title"), $"{LocalizedStr("version_warning")}\n\nTarget versions: {string.Join(", ", TargetFGVersions)} | FallGuys version: {Application.version}", UIModalMessage.ModalType.MT_OK, UIModalMessage.OKButtonType.CallToAction, closeDelay: 10f);
                verAlert = true;
            }

            //if (offlineMode.Value && !missingFilesAct && StateManager.FGTCurrentState == FGTStateEnum.BeforeMenu && PopupManager.Instance != null && PopupManager.Instance.ActivePopup != null && PopupManager.Instance.ActivePopup.Title == CMSLoader.Instance._localisedStrings._localisedStrings["missing_files_popup_title"])
            //{
            //    FGTLog(LogLevel.Info, base.GetType(), "telling missing files to shut up...");
            //    void pop(bool wasok)
            //    { }
            //    Il2CppSystem.Action<bool> act = new Action<bool>(pop);
            //    PopupManager.Instance.ActivePopup.Cast<ModalMessagePopupViewModel>()._onCloseButtonPressed = act;
            //    PopupManager.Instance.ActivePopup.Cast<ModalMessagePopupViewModel>().Close(true);
            //    missingFilesAct = true;
            //}

            if (!splashChanged)
            {
                if (File.Exists(Plugin.LoadingSplash))
                    Resources.FindObjectsOfTypeAll<LoadingScreenViewModel>().FirstOrDefault().gameObject.transform.FindChild("SplashScreen_Image").gameObject.GetComponent<UnityEngine.UI.Image>().sprite = SetSpriteFromFile(Plugin.LoadingSplash, 1920, 1080);
                else
                    FGTLog(LogLevel.Fatal, base.GetType(), "why did you deleted loading splash...");
                splashChanged = true;
            }

            if (catapultServicesManager == null)
                catapultServicesManager = Resources.FindObjectsOfTypeAll<CatapultServicesManager>().FirstOrDefault();

            if (activeScene == "MainMenu")
            {
                if (menuManager == null)
                {
                    menuManager = Resources.FindObjectsOfTypeAll<MainMenuManager>().FirstOrDefault();
                    //LoadFmodBank("BNK_Music_MainMenu");
                }

                //if (offlineMode.Value)
                //{
                //    if (CMSLoader.Instance._roundsSO.Rounds != null && CMSLoader.Instance._showsSO.Shows != null && !offlineLoginPart2)
                //        finishOfflineLogin();

                //    void finishOfflineLogin()
                //    {
                //        try { menuManager.OnTitleScreenComplete(); } catch { }
                //        catapultServicesManager.StopAllCoroutines();
                //        menuManager.OnMainMenuEntered(false, true);
                //        FGTLog(LogLevel.Info, base.GetType(), "Offline login complete");
                //        StateManager.CanUseHotkeys = true;
                //        StateManager.InternalState.loaderUIToggle = true;
                //        offlineLoginPart2 = true;
                //        spinnerloading.HideScreen();
                //    }


                //    if (!offlineLoginPart1 && !offlineLoginPart2 && menuManager != null && catapultServicesManager != null && catapultServicesManager.ContentService != null)
                //    {
                //        if (!offlineLoginPart1)
                //        {
                //            spinnerloading = Resources.FindObjectsOfTypeAll<UIManager>().FirstOrDefault().ShowScreen<LoadingSpinnerScreenViewModel>(new ScreenMetaData { Transition = ScreenTransitionType.FadeInAndOut, ScreenStack = ScreenStackType.Default, });
                //            FGTLog(LogLevel.Info, base.GetType(), "Starting offline login");
                //            if (!StateManager.InternalState.offlinePatches)
                //            {
                //                Plugin.offlineHarmony.PatchAll(typeof(OfflineOnlyPatches));
                //                StateManager.InternalState.offlinePatches = true;
                //            }
                //            if (!File.Exists(Application.persistentDataPath + "\\content_v2.gdata"))
                //            {
                //                FGTLog(LogLevel.Warning, base.GetType(), "content_v2 is missing, default one will be used!");
                //                File.Copy(Plugin.assetsDir + "\\content_v2.gdata", Application.persistentDataPath + "\\content_v2.gdata");
                //                try { catapultServicesManager.HandleConnected(); } catch { }
                //            }
                //            else
                //                try { catapultServicesManager.HandleConnected(); } catch { }
                //            offlineLoginPart1 = true;
                //        }
                //    }
                //}
            }
        }

        public override void DisplayGUI()
        {

        }
    }
}