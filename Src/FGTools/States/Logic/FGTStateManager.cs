extern alias wle;

using BepInEx.Logging;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using Events;
using FG.Common;
using FG.Common.Character;
using FG.Common.CMS;
using FG.Common.Fraggle;
using FGClient;
using FGClient.UI;
using FGTools.Config;
using FGTools.HarmonyPatches;
using FGTools.Internal;
using FGTools.Internal.Behaviours;
using FGTools.Internal.Extensions;
using FGTools.Services;
using Levels.Progression;
using Mediatonic.Tools.MVVM;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using UnityEngine;
using UnityEngine.SceneManagement;
using wle::ScriptableObjects;
using static FGClient.GlobalGameStateClient;
using static FGClient.UI.UIModalMessage;
using static FGTools.Config.ConfigManager;
using static FGTools.Internal.Extensions.FLZ_Extensions;
using static FGTools.Services.LocalizationService;
using static FGTools.Services.MenuThemeService;
using static GameStateEvents;

namespace FGTools.States.Logic
{
    internal class FGTStateManager : FGTBase
    {
        public static FGTStateManager _stateManager;

        internal static string AssemblyHash;
        internal InternalState InternalState = new();
        internal FGTState ActiveState;
        internal FGTState PreviousState;
        internal UltimatePartyState ExploreState;
        internal ShowState ShowState;
        internal static PlayerTeamManager PTM => CGM._playerTeamManager;
        internal static InGameUiManager UIM => CGM._inGameUiManager;
        internal StateGameLoading GameLoading;

        internal Round CurrentRound;
        internal Round PreviousRound;

        internal bool PiratedGame = false;
        internal bool IsFGC => CurrentRound != null && CurrentRound.IsUGC();
        internal bool IsPlayingExplore => ExploreState != null;
        internal bool IsInGameplay => FGTCurrentState == ToolsState.GameActive || FGTCurrentState == ToolsState.FGCGameActive;
        internal bool IsInEditor => FGTCurrentState == ToolsState.InCreative;
        internal bool IsIntroPlaying => FGTCurrentState == ToolsState.RoundIntro;

        internal bool LoggedInBefore = false;
        internal bool CanUseHotkeys = false;

        internal static string TargetFontName
        {
            get
            {
                if (UseCustomFonts.Value)
                {
                    if (ConfigManager.LangFileName.Value == "ru")
                        return "PT Astra Sans_Bold (Body)";
                    else if (ConfigManager.LangFileName.Value == "ko" || ConfigManager.LangFileName.Value == "jp" || ConfigManager.LangFileName.Value == "cn")
                        return "NotoSansCJKsc-Medium (Body)";
                    else
                        return "FGAsap-Bold (Body)";
                }
                else
                    return "";
            }
        }

        internal bool RoundLoadingAllowed = false;

        public enum ToolsState
        {
            BeforeMenu,
            CMSParsed,
            Menu,
            SceneLoaded,
            GPFGCLoading,
            RoundLoading,
            RoundIntro,
            IntroComplete,
            GameActive,
            FGCGameActive,
            GameEnded,
            GamePaused,
            InCreative,
            ConfigIssue,
            Results,
            OnlineGameActive,
        }

        public enum PlayerState
        {
            Despawned,
            Active,
            Finish,
            FreeFly,
            FreeCam,
        }

        public ToolsState FGTCurrentState;
        public PlayerState FGCurrentState;

        public Dictionary<string, HashSet<string>> BuildScenes = [];
        public float TimeInState;
        internal GameObject CheckpointModel;
        internal Process GameProcess;
        private Thread _memoryThread;
        internal double PeakMemUsage;
        internal double MemUsage;

        internal static bool IsInIllegalState
        {
            get
            {
                var scene = SceneManager.GetActiveScene();
                if (scene.name == "Transition" || scene.name == "Boot" || scene.name == "EmptyLoadingScene" || scene.name == "Init")
                    return true;

                var gsm = GlobalGameStateClient.Instance?._gameStateMachine;
                if (gsm == null) return false;

                return gsm.IsInState<StateConnectionAuthentication>() ||
                    gsm.IsInState<StateGameLoading>() ||
                    gsm.IsInState<StateDisconnectingFromServer>() ||
                    gsm.IsInState<StateMatchmaking>() ||
                    gsm.IsInState<StatePrivateLobby>() ||
                    gsm.IsInState<StateReloadingToMainMenu>();
            }
        }


        public FGTStateManager()
        {
            if (_stateManager != null)
                _stateManager = null;

            _stateManager = this;

            BepInEx.Logging.Logger.Sources.Add(logSource);

            FGTServiceManager.RegisterServices();

            Broadcaster.Instance.Register<IntroCountdownEndedEvent>(new Action<IntroCountdownEndedEvent>(OnRoundStart));
            Broadcaster.Instance.Register<OnRoundOver>(new Action<OnRoundOver>(OnRoundEnd));
            Broadcaster.Instance.Register<IntroCameraSequenceStartedEvent>(new Action<IntroCameraSequenceStartedEvent>(OnIntroStart));
            Broadcaster.Instance.Register<IntroCameraSequenceEndedEvent>(new Action<IntroCameraSequenceEndedEvent>(OnIntroEnd));
            Broadcaster.Instance.Register<OnMainMenuDisplayed>(new Action<OnMainMenuDisplayed>(OnEnterMenu));
            Broadcaster.Instance.Register<OnLocalPlayersFinished>(new Action<OnLocalPlayersFinished>(OnFinish));
            Broadcaster.Instance.Register<InitialiseClientOverlayEvent>(new Action<InitialiseClientOverlayEvent>(OverlayInit));

            using var stream = File.OpenRead(Path.Combine(Application.dataPath, "..", "GameAssembly.dll"));
            AssemblyHash = BitConverter.ToString(SHA256.Create().ComputeHash(stream)).Replace("-", "").ToLowerInvariant();

            var theme = $"{Launcher.ThemesDir}/{InGameTheme.Value}";

            if (File.Exists(theme))
            {
                if (!Launcher.ThemesHarmonyPatched)
                {
                    Launcher.ThemesHarmony.PatchAll(typeof(ThemePatches));
                    Launcher.ThemesHarmonyPatched = true;
                }

                var themeService = FGTServiceManager.GetService<MenuThemeService>();

                themeService.ThemeOnPreviewPath = InGameTheme.Value;
                themeService.CurrentTheme = JsonSerializer.Deserialize<Theme>(File.ReadAllText(theme));

            }
            else
            {
                InGameTheme.Value = LocalizedStr("gui_default");
                FGTLog(LogLevel.Warning, base.GetType(), "Theme files are missing or not set yet, fallback to default.");
            }

            if (File.Exists(Launcher.BundlePath))
            {
                Launcher.FGToolsBundle = AssetBundle.LoadFromFile(Launcher.BundlePath);

                //if (UseCustomFonts.Value)
                //    InternalState.TargetFont = Plugin.FGToolsBundle.LoadAssetAsync<Font>(TargetFontName).asset.Cast<Font>();
            }

            CheckupScenes();

            GameActions.OnMenuEnter += new Action(() =>
            {
                if (CheckpointModel != null)
                    return;

                CoroutineRunner.Instance.StartCoroutine(LoadObject<PlaceableVariant_Prefab>("POD_FloorStart_Survival_SpawnPoint", new((res) =>
                {
                    CheckpointModel = GameObject.Instantiate(res.Prefab.gameObject.GetComponentInChildren<Renderer>().gameObject);
                    CheckpointModel.hideFlags = HideFlags.HideAndDontSave;
                    CheckpointModel.gameObject.SetActive(false);
                    GameObject.DontDestroyOnLoad(CheckpointModel);
                    CheckpointModel.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
                })).WrapToIl2Cpp());
            });

            GameProcess = Process.GetCurrentProcess();
            _memoryThread = new(() =>
            {
                while (true)
                {
#if !DEV
                    if (!DebugDisplayService.UIToggle)
                        continue;
#endif
                    GameProcess.Refresh();
                    MemUsage = GameProcess.WorkingSet64 / 1024.0 / 1024.0;
                    if (MemUsage > PeakMemUsage)
                        PeakMemUsage = MemUsage;

                    Thread.Sleep(150);
                }
            })
            {
                IsBackground = true
            };
            _memoryThread.Start();

            ForceSetState(new MenuState());
        }

        void CheckupScenes()
        {
            try
            {
                if (File.Exists(Launcher.BuildScenesList))
                    BuildScenes = JsonSerializer.Deserialize<Dictionary<string, HashSet<string>>>(File.ReadAllText(Launcher.BuildScenesList));
            }
            catch
            {

            };

            if (BuildScenes.ContainsKey(AssemblyHash))
                return;

            FGTLog(LogLevel.Info, GetType(), "Starting scenes checkup...");
            var sw = new Stopwatch();
            sw.Start();

            BuildScenes.Clear();
            var list = new HashSet<string>();

            foreach (var path in Directory.GetFiles(Application.streamingAssetsPath + "/aa/StandaloneWindows64/", "*.bundle", SearchOption.AllDirectories))
            {
                try
                {
                    var bundle = AssetBundle.LoadFromFile(path);
                    if (bundle != null)
                    {
                        foreach (var scene in bundle.GetAllScenePaths())
                            list.Add(scene);

                        bundle.Unload(false);
                    }
                }
                catch
                {

                }
            }

            BuildScenes.Add(AssemblyHash, list);

            File.WriteAllText(Launcher.BuildScenesList, JsonSerializer.Serialize(BuildScenes));
            sw.Stop();

            FGTLog(LogLevel.Info, this.GetType(), $"Total scenes in this build ({Application.version} | {ClientBuildDetails.PlatformServiceProvider}): {BuildScenes[AssemblyHash].Count}. Checkup took: {sw.Elapsed.TotalSeconds:F3}s");
        }

        void OnEnterMenu(OnMainMenuDisplayed evt) => GameActions.OnMenuEnter?.Invoke();
        void OnFinish(OnLocalPlayersFinished evt) => GameActions.OnFinished?.Invoke();
        void OnRoundStart(IntroCountdownEndedEvent evt) => GameActions.OnRoundStarts?.Invoke();
        void OnRoundEnd(OnRoundOver evt) => GameActions.OnRoundEnds?.Invoke();
        void OnIntroStart(IntroCameraSequenceStartedEvent evt) => GameActions.OnIntroStarts?.Invoke();
        void OnIntroEnd(IntroCameraSequenceEndedEvent evt) => GameActions.OnIntroEnds?.Invoke();
        void OverlayInit(InitialiseClientOverlayEvent evt) => GameActions.OnOverlayInitialize?.Invoke(evt);

        public void TryJoinExplore(UltimatePartyState.JoinPolicy joinType)
        {
            if (ExploreState == null)
            {
                if (joinType == UltimatePartyState.JoinPolicy.FGC && FGTRoundLoader.FGCRateLimit)
                    return;

                ExploreState = new(joinType);
                ExploreState.OnStateSet();
            }
            else
            {
                if (ExploreState.CurrentJoinPolicy == joinType)
                {
                    ExploreState.RequestNewRound();
                    return;
                }

                ExploreState = null;
                TryJoinExplore(joinType);
            }   
        }

        public void TryJoinShowGameplay(string show)
        {
            if (ShowState == null)
            {
                ShowState = new();
                ShowState.OnNewShowSet(show);
            }
        }

        public void QuitExplore()
        {
            ExploreState.OnExploreQuit();
            ExploreState = null;
        }

        public void SetNewRound(Round round)
        {
            PreviousRound = CurrentRound;
            CurrentRound = round;
        }

        public T GetState<T>() where T : Logic.FGTState
        {
            return ActiveState as T;
        }

        public void ForceSetState(Logic.FGTState state)
        {
            FGTLog(LogLevel.Info, GetType(), $"Changing state to {state.GetType().Name}");

            ActiveState?.OnStateExit();
            PreviousState = ActiveState;
            ActiveState = state;
            state.OnStateSet();
            TimeInState = 0;

            if (state is MenuState)
                HandleFGTState(ToolsState.BeforeMenu);

            GC.Collect();
        }

        public void SetState(Logic.FGTState state)
        {
            if (ActiveState == null)
                ForceSetState(state);
            else if(ActiveState.GetType() != state.GetType())
                ForceSetState(state);

        }

        public void DropActiveState()
        {
            FGTLog(LogLevel.Info, base.GetType(), "Dropping current state.");
            ActiveState.OnStateExit();
            ActiveState = null;
            TimeInState = 0;
            GC.Collect();
        }

        public void HandleFGState(PlayerState newState)
        {
            if (newState != PlayerState.FreeCam && FGCurrentState == PlayerState.FreeCam)
                FallGuyBehaviour._instance.fc.ExitFC();
            FGCurrentState = newState;

            switch (newState)
            {
                case PlayerState.FreeCam:
                    if (!FGTServiceManager.GetService<EventService>().ReturnBoolEventValue("FCTip"))
                    {
                        CreateNotification(LocalizedStr("msg_tip"), LocalizedStr("msg_tip_fc", [PauseFreeCamHotkey.Value, FreeCamToggleUI.Value]), FGT_Info_Color);
                        FGTServiceManager.GetService<EventService>().SetEventValue("FCTip", true);
                    }
                    FallGuyBehaviour._instance.fc.EnterFC();
                    break;
                case PlayerState.Finish:
                    HandleFGTState(ToolsState.GameEnded);
                    if (CGM != null)
                    {
                        CGM._gameSession.SetSessionState(FG.Common.GameSession.SessionState.Finished);
                        CGM._readinessState = FG.Common.PlayerReadinessState.LevelLoaded;
                    }
                    break;
                case PlayerState.FreeFly:
                    if (!FGTServiceManager.GetService<EventService>().ReturnBoolEventValue("FFMTip"))
                    {
                        CreateNotification(LocalizedStr("msg_tip"), LocalizedStr("msg_tip_ffm"), FGT_Info_Color);
                        FGTServiceManager.GetService<EventService>().SetEventValue("FFMTip", true);
                    }
                    break;
            }
        }

        public void HandleFGTState(ToolsState newState)
        {
            FGTCurrentState = newState;

            GameActions.OnStateChange?.Invoke(newState);

            switch (newState)
            {
                case ToolsState.CMSParsed:
                    PlayerTargetSettings.AnalyticsEnabled = false;
                    PlayerTargetSettings.NewAnalyticsTimeTrackerEnabled = false;
                    foreach (ActiveBinding a in Resources.FindObjectsOfTypeAll<ActiveBinding>())
                    {
                        if (a._sourcePropertyName == "CanEnablePlayAgainButton")
                        {
                            a.enabled = false;
                            a.gameObject.SetActive(false);
                        }
                    }
                    if (FGTServiceManager.GetService<MenuThemeService>().CurrentTheme != null)
                    {
                        PlayerTargetSettings.Menu3DBackgroundEnabled = false;
                        FGTServiceManager.GetService<MenuThemeService>().OnMenuSetThemeEvent(true);
                    }

                    break;
                case ToolsState.Menu:
                    FraggleCommonManager.Instance.IsInLevelEditor = false;
                    FraggleCommonManager.Instance.SetModeToBuild(new());
                    FGTServiceManager.GetService<RoundLoaderService>().UsingAdditiveLoad = false;
                    CanUseHotkeys = true;
                    InternalState.LoaderUIToggle = true;
                    NetworkGameData.ClearCurrentGameOptions();
                    SetState(new MenuState());
                    RoundLoadingAllowed = true;
                    break;
                case ToolsState.GamePaused:
                    CGM?.CurrentGameSession.SetSessionState(GameSession.SessionState.Precountdown);
                    break;
                case ToolsState.GameActive:
                    if (CGM != null && CGM.CurrentGameSession.CurrentSessionState != GameSession.SessionState.Playing)
                        CGM.CurrentGameSession.SetSessionState(GameSession.SessionState.Playing);
                    break;
                case ToolsState.InCreative:
                    if (!Launcher.FGCHarmonyPatched)
                    { 
                        Launcher.FGCHarmony.PatchAll(typeof(FGCHarmonyPatches)); 
                        Launcher.FGCHarmonyPatched = true; 
                    }
                    if (Launcher.HarmonyPatched)
                    { 
                        Launcher.GlobalHarmony.UnpatchSelf(); 
                        Launcher.HarmonyPatched = false; 
                    }
                    if (!FGTServiceManager.GetService<EventService>().ReturnBoolEventValue("RpcFGCAlert"))
                    {
                        DoModal(new(LocalizedStr("rpc_fgc_alert_title"), LocalizedStr("rpc_fgc_alert_desc"), ModalType.MT_OK, OKButtonType.Default));
                        FGTServiceManager.GetService<EventService>().SetEventValue("RpcFGCAlert", true);
                    }
                    break;
                case ToolsState.GPFGCLoading:
                    if (Launcher.HarmonyPatched)
                    { Launcher.GlobalHarmony.UnpatchSelf(); Launcher.HarmonyPatched = false; }
                    break;
            }
        }

        public void Update()
        {
            TimeInState += Time.unscaledDeltaTime;

            InternalState?.UpdateState();
            ActiveState?.UpdateState();
        }


        public void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            InternalState?.OnSceneLoaded(scene, mode);
            ActiveState?.OnSceneLoaded(scene, mode);
        }

        public void DrawGUI()
        {
            InternalState?.DisplayGUI();
            ActiveState?.DisplayGUI();
        }
    }
}
