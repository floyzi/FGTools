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
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
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
        public InternalState InternalState = new();
        public Logic.FGTState ActiveState;
        public Logic.FGTState PreviousState;
        public UltimatePartyState ExploreState;
        public ShowState ShowState;
        public PlayerTeamManager PTM => CGM._playerTeamManager;
        public InGameUiManager UIM => CGM._inGameUiManager;
        public StateGameLoading GameLoading;

        public Round CurrentRound;
        public Round PreviousRound;

        public bool PiratedGame = false;
        public bool IsFGC = false;
        public bool IsPlayingExploreFGC = false;
        public bool FirstTimeLogin = false;
        public bool CanUseHotkeys = false;
        public bool HaveActivePopup = false;

        public bool IsInGameplay { get { return FGTCurrentState == FGTState.GameActive || FGTCurrentState == FGTState.FGCGameActive; } }
        public bool IsInEditor { get { return FGTCurrentState == FGTState.InCreative; } }
        public bool IsInExplore { get { return ExploreState != null; } }

        public static string TargetFontName
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

        public bool RoundLoadingAllowed = false;

        public enum FGTState
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
        }

        public enum PlayerState
        {
            Despawned,
            Active,
            Finish,
            FreeFly,
            FreeCam,
        }

        public FGTState FGTCurrentState;
        public PlayerState FGCurrentState;

        public Dictionary<string, HashSet<string>> BuildScenes = [];
        public float TimeInState;
        internal GameObject CheckpointModel;

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

            var theme = $"{Plugin.ThemesDir}/{InGameTheme.Value}";

            if (File.Exists(theme))
            {
                if (!Plugin.ThemesHarmonyPatched)
                {
                    Plugin.ThemesHarmony.PatchAll(typeof(ThemePatches));
                    Plugin.ThemesHarmonyPatched = true;
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

            if (File.Exists(Plugin.BundlePath))
            {
                Plugin.FGToolsBundle = AssetBundle.LoadFromFile(Plugin.BundlePath);

                //if (UseCustomFonts.Value)
                //    InternalState.TargetFont = Plugin.FGToolsBundle.LoadAssetAsync<Font>(TargetFontName).asset.Cast<Font>();
            }

            CheckupScenes();

            Commands.OnMenuEnter += new Action(() =>
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

            ForceSetState(new MenuState());
        }

        void CheckupScenes()
        {
            if (File.Exists(Plugin.BuildScenesList))
                BuildScenes = JsonSerializer.Deserialize<Dictionary<string, HashSet<string>>>(File.ReadAllText(Plugin.BuildScenesList));

            if (BuildScenes.ContainsKey(AssemblyHash))
                return;

            BuildScenes.Clear();
            var list = new HashSet<string>();
            foreach (var path in Directory.GetFiles(Application.streamingAssetsPath + "/aa/StandaloneWindows64/", "*.bundle", SearchOption.AllDirectories))
            {
                try
                {
                    var bundle = AssetBundle.LoadFromFile(path);
                    if (bundle != null)
                    {
                        var scenePath = bundle.GetAllScenePaths().FirstOrDefault();
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

            File.WriteAllText(Plugin.BuildScenesList, JsonSerializer.Serialize(BuildScenes));

            FGTLog(LogLevel.Info, this.GetType(), $"Total scenes in this build ({Application.version} | {ClientBuildDetails.PlatformServiceProvider}): {BuildScenes.Count}");
        }

        void OnEnterMenu(OnMainMenuDisplayed evt) => Commands.OnMenuEnter?.Invoke();
        void OnFinish(OnLocalPlayersFinished evt) => Commands.OnFinished?.Invoke();
        void OnRoundStart(IntroCountdownEndedEvent evt) => Commands.OnRoundStarts?.Invoke();
        void OnRoundEnd(OnRoundOver evt) => Commands.OnRoundEnds?.Invoke();
        void OnIntroStart(IntroCameraSequenceStartedEvent evt) => Commands.OnIntroStarts?.Invoke();
        void OnIntroEnd(IntroCameraSequenceEndedEvent evt) => Commands.OnIntroEnds?.Invoke();
        void OverlayInit(InitialiseClientOverlayEvent evt) => Commands.OnOverlayInitialize?.Invoke(evt);

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
                HandleFGTState(FGTState.BeforeMenu);

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
                    HandleFGTState(FGTState.GameEnded);
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

        public void HandleFGTState(FGTState newState)
        {
            FGTCurrentState = newState;

            Commands.OnStateChange?.Invoke(newState);

            switch (newState)
            {
                case FGTState.CMSParsed:
                    PlayerTargetSettings.UGCLikesEnabled = false;
                    PlayerTargetSettings.UGCThumbnailReportingEnabled = false;
                    PlayerTargetSettings.VoiceChatEnabled = false;
                    PlayerTargetSettings.PlayerReportEnabled = false;
                    PlayerTargetSettings.ShowSelectorEnabled = false;
                    PlayerTargetSettings.ChallengesEnabled = false;
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
                case FGTState.Menu:
                    IsPlayingExploreFGC = false;
                    IsFGC = false;
                    FraggleCommonManager.Instance.IsInLevelEditor = false;
                    FraggleCommonManager.Instance.SetModeToBuild(new());
                    FGTServiceManager.GetService<RoundLoaderService>().UsingAdditiveLoad = false;
                    CanUseHotkeys = true;
                    InternalState.LoaderUIToggle = true;
                    NetworkGameData.ClearCurrentGameOptions();
                    SetState(new MenuState());
                    RoundLoadingAllowed = true;
                    break;
                case FGTState.GamePaused:
                    CGM?.CurrentGameSession.SetSessionState(GameSession.SessionState.Precountdown);
                    break;
                case FGTState.GameActive:
                    IsFGC = false;
                    if (CGM != null && CGM.CurrentGameSession.CurrentSessionState != GameSession.SessionState.Playing)
                        CGM.CurrentGameSession.SetSessionState(GameSession.SessionState.Playing);
                    break;
                case FGTState.InCreative:
                    if (!Plugin.FGCHarmonyPatched)
                    { 
                        Plugin.FGCHarmony.PatchAll(typeof(FGCHarmonyPatches)); 
                        Plugin.FGCHarmonyPatched = true; 
                    }
                    if (Plugin.HarmonyPatched)
                    { 
                        Plugin.GlobalHarmony.UnpatchSelf(); 
                        Plugin.HarmonyPatched = false; 
                    }
                    if (!FGTServiceManager.GetService<EventService>().ReturnBoolEventValue("RpcFGCAlert"))
                    {
                        DoModal(LocalizedStr("rpc_fgc_alert_title"), LocalizedStr("rpc_fgc_alert_desc"), ModalType.MT_OK, OKButtonType.Default);
                        FGTServiceManager.GetService<EventService>().SetEventValue("RpcFGCAlert", true);
                    }
                    break;
                case FGTState.GPFGCLoading:
                    if (Plugin.HarmonyPatched)
                    { Plugin.GlobalHarmony.UnpatchSelf(); Plugin.HarmonyPatched = false; }
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
