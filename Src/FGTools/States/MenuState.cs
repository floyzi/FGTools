extern alias wle;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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
using FGTools.Internal.Extensions;
using FGTools.Services;
using FGTools.States.Logic;
using FGTools.UI;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using NAudio.Wave;
using Rewired;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;
using UniverseLib.UI;
using static FGTools.Config.Config;
using static FGTools.Internal.Extensions.FLZ_Extensions;
using static FGTools.Services.LocalizationService;
using static FGTools.UI.ReadyPopups;

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

        void MakeTooltips()
        {
            try
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
            catch
            {

            }
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

            FGTLog(LogLevel.Info, base.GetType(), "Time for... GAMING INTRO");

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

            FGTLog(LogLevel.Info, base.GetType(), $"Intro complete, took={IntroStopwatch.Elapsed.Seconds:F2}s");

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
                Action firstLaunch = () =>
                {
                    if (!StateManager.LoggedInBefore)
                    {
                        Launcher.UniverseUIBase ??= UniversalUI.RegisterUI(UniverseGUID, null);

                        if (FGToolsUI.NewGUI.Instance == null)
                            StateManager.InternalState.ToolsUI = new(Launcher.UniverseUIBase);
                    }
#if DEV
                    Broadcaster.Instance.Broadcast<GlobalDebug.DebugToggleFPSCounter>(new());
#endif

                    StateManager.InternalState.ToolsUI.ToggleUI(true);
                    Launcher.UniverseUIBase.SetOnTop();

                    StateManager.LoggedInBefore = true;

                    FGTServiceManager.GetService<RoundLoaderService>().SetupCMSRoundList();
                    FGTServiceManager.GetService<StatisticsService>().ValidateStats(GlobalGameStateClient.Instance.PlayerProfile.PlatformAccountName);
                    FGTServiceManager.GetService<CosmeticsService>().Load();
                };

                StateManager.CanUseHotkeys = true;
                StateManager.ExploreState = null;
                StateManager.ShowState = null;
                StateManager.RoundLoadingAllowed = true;

                MakeTooltips();

                CheckFirstLaunchFlow(firstLaunch);
               
                StateManager.HandleFGTState(FGTStateManager.ToolsState.Menu);
                FinishLoginAct = null;
            });

            var targetVer = Launcher.BuildInfo.UI_Version;
            var evtVer = FGTServiceManager.GetService<EventService>().ReturnStringEventValue("MenuEntranceVersion");

            if (evtVer != null && targetVer != evtVer)
                CoroutineRunner.Instance.StartCoroutine(PlayIntroAndContinue().WrapToIl2Cpp());
            else
                FinishLoginAct();
        }

        void CheckFirstLaunchFlow(Action onOver)
        {
            var evtS = FGTServiceManager.GetService<EventService>();

            var targetVer = Launcher.BuildInfo.UI_Version;
            var eventVer = evtS.ReturnStringEventValue("MenuEntranceVersion");

            if (string.IsNullOrEmpty(eventVer) || targetVer != eventVer)
            {
                DoModal(new(LocalizedStr("menuenter_title"), $"{LocalizedStr("menuenter_desc")}\n\n{LocalizedStr("gui_hotkeys", [ToggleCusorHotkey.Value, ToggleUIHotkey.Value, DebugUIHotkey.Value, EnterFFM.Value, RespawnHotkey.Value, CheckpointHotkey.Value, ToggleFreeCamHotkey.Value, ResetCheckpointHotkey.Value])}", UIModalMessage.ModalType.MT_OK, UIModalMessage.OKButtonType.Positive, onClosed: new Action<bool>((wasOk) =>
                {
                    if (OnlineCheck.TryBuildChangelog(Launcher.BuildInfo.UI_Version, out var log))
                    {
                        CreateEULAModal(string.Format($"V{Launcher.BuildInfo.UI_Version} - {LocalizedStr("changelog_title")}"), log, new Action<bool>(wasok =>
                        {
                            onOver();
                        }), true);
                    }
                    else
                    {
                        FGTLog(LogLevel.Warning, GetType(), $"No changelog were found for version {Launcher.BuildInfo.UI_Version}");
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