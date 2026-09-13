extern alias wle;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using DG.Tweening;
using Events;
using FG.Common.CMS;
using FGClient;
using FGClient.UI;
using FGDebug;
using FGTools.Content;
using FGTools.Internal;
using FGTools.Internal.Behaviours;
using FGTools.Internal.Extensions;
using FGTools.Services;
using FGTools.States.Logic;
using FGTools.UI;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using NAudio.Wave;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;
using static FGTools.Config.Config;
using static FGTools.Internal.Extensions.FLZ_Extensions;
using static FGTools.Services.LocalizationService;

namespace FGTools.States
{
    public class MenuState : FGTState
    {
        static MainMenuManager MenuManager => GlobalGameStateClient.Instance._mainMenuManager;
        AssetBundleRequest TheIntro;
        bool verAlert = false;
        internal Stopwatch IntroStopwatch = new();
        Action FinishLoginAct;
        const float IntroTimeout = 30f;
        public override void OnStateExit()
        {
            GameActions.OnMenuEnter -= OnMenuEnter;
            Resources.FindObjectsOfTypeAll<MainMenuManager>().FirstOrDefault()?.StopMusic();
        }

        IEnumerator PlayIntroAndContinue()
        {
            if (FinishLoginAct == null)
            {
                FLZ_Extensions.QuitWithMessage("Error", "Tried to play intro without finish action, this is not intended and will cause a softlock");
                yield break;
            }

            if (!FGTTargetSettings.GamingIntro || !File.Exists(Launcher.IntroMusic))
            {
                FinishLoginAct();
                yield break;
            }

            if (IntroStopwatch.IsRunning)
                yield break;

            if (TheIntro.asset == null)
            {
                FinishLoginAct();
                yield break;
            }

            IntroStopwatch.Start();

            FGTLog(LogLevel.Info, GetType(), "Time for... GAMING INTRO");

            //temp fix
            FGToolsUI.Instance?.ToggleUI(false);

            var loopEvent = new WaveOutEvent();
            var outp = GameObject.Instantiate(TheIntro.asset);

            yield return outp;

            if (MenuAudioProvider.instance == null)
                MenuManager.PauseMusic(true);
            else
            {
                MenuManager.StopMusic();
                MenuAudioProvider.instance.StopMusic();
                FMODTool.UnloadBank("BNK_Music_MainMenu");
            }

            var loopVol = new VolumeWaveProvider16(new WaveFileReader(Launcher.IntroMusic)) { Volume = GlobalGameStateClient.Instance.PlayerProfile.AudioSettings.MusicVolume * GlobalGameStateClient.Instance.PlayerProfile.AudioSettings.MasterVolume };
            yield return loopVol;

            loopEvent.Init(loopVol);

            var res = GameObject.Find("CoolGamingIntro(Clone)");
            var intro = res.transform.GetChild(0).gameObject.GetComponent<VideoPlayer>();
            var filler = res.transform.GetChild(2).gameObject;

            yield return new WaitForSeconds(0.35f);

            loopEvent.Play();
            intro.Play();
            filler.gameObject.SetActive(false);
            RewiredManager.Instance.DisableMap(1);

            if (intro.length >= IntroTimeout)
            {
                FGTLog(LogLevel.Fatal, GetType(), "Intro length above the timeout!!");
                FinishLoginAct();
                yield break;
            }

            yield return new WaitForSeconds((float)intro.length);

            FGTLog(LogLevel.Info, GetType(), $"Intro complete, took={IntroStopwatch.Elapsed.Seconds:F2}s");

            IntroStopwatch.Stop();
            filler.gameObject.SetActive(true);

            FinishLoginAct();

            yield return new WaitForSeconds(0.5f);

            filler.gameObject.SetActive(false);
            var fade = 1.5f;
            res.transform.GetChild(1).transform.GetComponent<RawImage>().DOFade(0, fade);
            GameObject.Destroy(res.gameObject, fade + 0.5f);
            RewiredManager.Instance.EnableMap(1);

            if (MenuAudioProvider.instance == null)
                MenuManager.ResumeMusic();
            else
                MenuAudioProvider.instance.PlayMusic(true);

            yield break;
        }

        public override void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
           
        }

        public override void OnStateSet()
        {
            TheIntro = Launcher.FGToolsBundle.LoadAssetAsync<GameObject>("CoolGamingIntro");
            GameActions.OnMenuEnter += OnMenuEnter;
        }

        void OnMenuEnter()
        {
            FGTLog(LogLevel.Info, base.GetType(), "OnMenuEnter()");

            if (ShowsManager.Instance != null && ShowsManager.Instance.SelectedShowDef != null)
            {
                ShowData foundShow = null;
                foreach (var show in ShowsManager.Instance.SelectedShowDef)
                {
                    if (show.Value)
                    {
                        foundShow = show.key;
                        break;
                    }
                }

                if (foundShow == null || foundShow.ShowSelectorShow == null || !foundShow.ShowSelectorShow.IsUltimatePartyShow || !foundShow.ShowSelectorShow.IsUltimatePartyRankedShow)
                    GlobalGameStateClient.Instance.StopUltimatePartyFlow();
            }

            FGTServiceManager.GetService<MenuThemeService>().OnMenuEnterEvent();
            FMODTool.UnloadAllLoadedBanks();

            if (LocalServerService.IsServerInOperation)
                FGTServiceManager.GetService<LocalServerService>().ShutdownSerer(null);

            FinishLoginAct = new(() =>
            {
                StateManager.CanUseHotkeys = true;
                StateManager.ExploreState = null;
                StateManager.ShowState = null;
                StateManager.RoundLoadingAllowed = true;

                StateManager.InternalState.ManageTooltips();

                CheckFirstLaunchFlow(() =>
                {
#if DEV
                    Broadcaster.Instance.Broadcast<GlobalDebug.DebugToggleFPSCounter>(new());
#endif
                    StateManager.CreateUIIfNeeded();

                    StateManager.InternalState.ToolsUI.ToggleUI(true);
                    Launcher.UniverseUIBase.SetOnTop();

                    StateManager.LoggedInBefore = true;

                    FGTServiceManager.GetService<RoundLoaderService>().SetupCMSRoundList();
                    FGTServiceManager.GetService<StatisticsService>().ValidateStats(GlobalGameStateClient.Instance.PlayerProfile.PlatformAccountName);
                    FGTServiceManager.GetService<CosmeticsService>().Load();
                });
               
                StateManager.HandleFGTState(FGTStateManager.ToolsState.Menu);
                FinishLoginAct = null;
            });

            var targetVer = FGToolsBuildDetails.Version;
            var evtVer = FGTServiceManager.GetService<EventService>().ReturnStringEventValue("MenuEntranceVersion");

            if (evtVer != null && targetVer != evtVer)
                CoroutineRunner.Instance.StartCoroutine(PlayIntroAndContinue().WrapToIl2Cpp());
            else
                FinishLoginAct();
        }

        void CheckFirstLaunchFlow(Action onOver)
        {
            var evtS = FGTServiceManager.GetService<EventService>();

            var targetVer = FGToolsBuildDetails.Version;
            var eventVer = evtS.ReturnStringEventValue("MenuEntranceVersion");

            if (string.IsNullOrEmpty(eventVer) || targetVer != eventVer)
            {
                DoModal(new(LocalizedStr("menuenter_title"), $"{LocalizedStr("menuenter_desc")}\n\n{LocalizedStr("gui_hotkeys", [ToggleCusorHotkey.Value, ToggleUIHotkey.Value, DebugUIHotkey.Value, EnterFFM.Value, RespawnHotkey.Value, CheckpointHotkey.Value, ToggleFreeCamHotkey.Value, ResetCheckpointHotkey.Value])}", UIModalMessage.ModalType.MT_OK, UIModalMessage.OKButtonType.Positive, onClosed: new Action<bool>((wasOk) =>
                {
                    if (OnlineCheck.TryBuildChangelog(FGToolsBuildDetails.Version, out var log))
                    {
                        CreateEULAModal(string.Format($"V{FGToolsBuildDetails.Version} - {LocalizedStr("changelog_title")}"), log, new Action<bool>(wasok =>
                        {
                            onOver();
                        }), true);
                    }
                    else
                    {
                        FGTLog(LogLevel.Warning, GetType(), $"No changelog were found for version {FGToolsBuildDetails.Version}");
                        onOver();
                    }
                }), hideLvl: ModalHideGUIType.KeepHidden, priority: 1000));
                evtS.SetEventValue("MenuEntranceVersion", targetVer.ToString());
            }
            else
                onOver();
        }

        public override void UpdateState()
        {
            string activeScene = SceneManager.GetActiveScene().name;

            if (activeScene == "Boot")
                return;

            if (IntroStopwatch.IsRunning && IntroStopwatch.Elapsed.Seconds > IntroTimeout)
            {
                FGTLog(LogLevel.Warning, base.GetType(), $"Reached timeout while playing intro... Timeout={IntroTimeout}s");

                IntroStopwatch.Stop();
                FinishLoginAct?.Invoke();
            }

            if (!TargetFGVersions.Contains(Application.version) && !verAlert && activeScene == "MainMenu" && FGTTargetSettings.VersionWarning)
            {
                CreateNotification(LocalizedStr("version_warning_title"), LocalizedStr("version_warning"), FGT_Warning_Color, 20f);
                verAlert = true;
            }
        }

        public override void DisplayGUI()
        {

        }
    }
}