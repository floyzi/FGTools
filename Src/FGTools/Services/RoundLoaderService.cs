extern alias wle;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using Events;
using FG.Common;
using FG.Common.Audio;
using FG.Common.Character.MotorSystem;
using FG.Common.CMS;
using FG.Common.Definition;
using FG.Common.Fraggle;
using FGClient;
using FGClient.Rendering.XRay;
using FGClient.UI;
using FGClient.UI.Core;
using FGTools.Content;
using FGTools.Internal;
using FGTools.Internal.Behaviours;
using FGTools.Services.Logic;
using FGTools.States;
using FGTools.States.Logic;
using FGTools.UI;
using Il2CppInterop.Runtime.Attributes;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UniverseLib;
using UniverseLib.UI.Models;
using static FG.Common.GameStateMachine;
using static FGTools.Config.ConfigManager;
using static FGTools.Internal.Extensions.FLZ_Extensions;
using static FGTools.Internal.Extensions.FLZ_UIExtensions;
using static FGTools.Services.LocalizationService;
using static FGTools.Services.SpeedrunService;
using static FGTools.States.Logic.FGTStateManager;
using static FGTools.UI.ReadyPopups;
using static GameStateEvents;
using Random = UnityEngine.Random;

namespace FGTools.Services
{
    internal class RoundLoaderService : FGTService, IFGTGUIHelper
    {
        public bool isXtremeRound = false;
        //public string lastSuccessfulRound;
        //public string lastParsedRound;
        public string RoundToLoad = "round_";
        public bool UsingAdditiveLoad = false;
        public CameraDirector RoundCamera;
        public string disabledSetting = null;
        DateTime lastPrepStart = DateTime.MinValue;
        public bool FGCRateLimit { get { return CurrRateLimit >= RateLimitTarget; } }

        DateTime FGCRateLimitUntil = DateTime.MinValue;
        const int RateLimitTarget = 5;
        int CurrRateLimit = 0;
        DateTime NextRateLimitDeinc = DateTime.MinValue;
        int RateLimitedTimes = 0;

        Dictionary<string, string> RoundName2Scene = new();
        Dropdown roundNamesDrop;
        Dropdown roundVariantsDrop;
        Text RoundsStats;
        int deletedRounds = 0;

        public override void RegisterService()
        {
            Commands.OnOverlayInitialize += OnGUIInit;
            Commands.OnIntroStarts += OnIntroStart;
            Commands.OnIntroEnds += OnIntroEnd;
        }

        void SearchForRound(string request)
        {
            Il2CppSystem.Collections.Generic.List<string> result = new Il2CppSystem.Collections.Generic.List<string>();
            request = request.ToLower();
            RoundToLoad = string.Empty;

            roundNamesDrop.ClearOptions();
            roundVariantsDrop.ClearOptions();

           InsertDefaultOption(roundNamesDrop);

            foreach (var elem in RoundName2Scene)
            {
                if (elem.Key.ToLower().Contains(request) || elem.Value.ToLower().Contains(request))
                    result.Add(elem.Key);
            }

            result.Sort();
            roundNamesDrop.AddOptions(result);

            OnRoundSelect(0);

            InsertDefaultOption(roundVariantsDrop);

            RoundsStats.text = $"{LocalizedStr("gui_found_results")}: {roundNamesDrop.options.Count - 1} | {LocalizedStr("gui_total_ids")}: {roundVariantsDrop.options.Count}";
            if (request.Length == 0)
                RoundsStats.text = $"{LocalizedStr("gui_total_rounds")}: {roundNamesDrop.options.Count - 1} ({LocalizedStr("gui_deleted_rounds")}: {deletedRounds}) | {LocalizedStr("gui_total_ids")}: {roundVariantsDrop.options.Count}";
        }


        void OnRoundSelect(int value)
        {
            roundVariantsDrop.gameObject.SetActive(false);

            if (roundNamesDrop.options.Count <= 0)
                return;

            var cleanRoundName = Regex.Replace(roundNamesDrop.options[value].text, @"<color=(.*?)>|</color>", "");
            string round = string.Empty;
            roundVariantsDrop.ClearOptions();

            if (RoundName2Scene.ContainsKey(cleanRoundName))
            {
                round = RoundName2Scene[cleanRoundName];
                roundVariantsDrop.AddOptions(GetRoundIdsForScene(round));
                roundVariantsDrop.value = 1;
                roundVariantsDrop.gameObject.SetActive(true);
            }

            ForceHideDropdown(roundNamesDrop);
            ForceHideDropdown(roundVariantsDrop);
        }

        Il2CppSystem.Collections.Generic.List<string> GetRoundIdsForScene(string sceneName)
        {
            Il2CppSystem.Collections.Generic.List<string> result = new();
            foreach (var round in CMSLoader.Instance._roundsSO.Rounds)
            {
                if (round.value.DisplayName != null && !round.value.IsUGC() && round.value.SceneData != null && round.value.GetSceneName() == sceneName)
                    result.Add($"{round.key}");
            }
            if (result.Count > 0)
            {
                InsertDefaultOption(result);
                RoundsStats.text = $"{LocalizedStr("gui_total_rounds")}: {roundNamesDrop.options.Count - 1} ({LocalizedStr("gui_deleted_rounds")}: {deletedRounds}) | {LocalizedStr("gui_total_ids")}: {result.Count}";
                return result;
            }
            else
            {
                FGTLog(LogLevel.Warning, "GetRoundIdsForScene", $"Unable to find CMS rounds with this scene {sceneName}");
                return null;
            }
        }

        void OnRoundIdsSelected(int value) => FGTRoundLoader.RoundToLoad = roundVariantsDrop.options[value].text;

        public void SetUIReferences(object[] data)
        {
            if (!FGTTargetSettings.RoundLoader)
                return;

            roundNamesDrop = (Dropdown)data[0];
            roundNamesDrop.onValueChanged.AddListener(OnRoundSelect);
            roundVariantsDrop = (Dropdown)data[1];
            roundVariantsDrop.onValueChanged.AddListener(OnRoundIdsSelected);
            var searchBar = (InputFieldRef)data[2];
            searchBar.OnValueChanged += SearchForRound;
            RoundsStats = (Text)data[3];

            RefreshRoundsInfo();
            PopulateRounds();
        }

        bool IsValidRound(Round round)
        {
            return round.DisplayName != null &&
                !round.IsUGC() &&
                round.SceneData != null &&
                !RoundName2Scene.ContainsValue(round.GetSceneName()) &&
                !round.Id.StartsWith("round_season_") &&
                !round.Id.Contains("test") &&
                round.Id != "round_hoops_blockade" &&
                !round.GetSceneName().Contains("test");
        }


        void RefreshRoundsInfo()
        {
            RoundName2Scene.Clear();

            foreach (var round in CMSLoader.Instance.CMSData.Rounds)
            {
                if (IsValidRound(round.Value))
                {
                    string roundName = round.value.DisplayName.Text;

                    if (roundName.IsNullOrEmpty())
                        roundName = LocalizedStr("gui_round_unnamed").ToUpper();

                    if (roundName.Contains("<br>"))
                        roundName = CleanStr(roundName);

                    if (round.Value.GetSceneName().Contains("PumpkinPie"))
                        roundName = CMSLoader.Instance._localisedStrings._localisedStrings["le_ss2_pistachio_title"];


                    if (!RoundName2Scene.ContainsKey(roundName))
                        RoundName2Scene.Add(roundName, round.Value.GetSceneName());
                }

            }
        }

        void PopulateRounds()
        {
            InsertDefaultOption(roundNamesDrop);

            if (RoundName2Scene == null ||  RoundName2Scene.Count == 0)
            {
                FGTLog(LogLevel.Warning, GetType(), "Tried to create round list while rounds info not loaded.");
                return;
            }

            Il2CppSystem.Collections.Generic.List<string> completeRoundList = new Il2CppSystem.Collections.Generic.List<string>();
            foreach (var round in RoundName2Scene)
            {
                string name;
                if (!StateManager.BuildScenes[FGTStateManager.AssemblyHash].Contains(round.Value))
                {
                    name = $"<color=grey>{round.Key}</color>";
                    deletedRounds++;
                }
                else
                    name = round.Key;

                completeRoundList.Add(name);
            }

            completeRoundList.Sort();
            roundNamesDrop.AddOptions(completeRoundList);
            RoundsStats.text = $"{LocalizedStr("gui_total_rounds")}: {roundNamesDrop.options.Count - 1} ({LocalizedStr("gui_deleted_rounds")}: {deletedRounds}) | {LocalizedStr("gui_total_ids")}: {roundVariantsDrop.options.Count}";
        }

        void OnGUIInit(InitialiseClientOverlayEvent evt)
        {
            //FGTBehaviour.StartCoroutine(PrepareGame("").WrapToIl2Cpp());
        }

        void OnIntroStart()
        {
            StateManager.ForceSetState(new GameplayState());

            if (CGM._round.Archetype.Id.Contains("attack"))
                FGTServiceManager.GetService<SpeedrunService>().SpeedrunState = RunState.TimeAttack;
            else if (StateManager.ExploreState != null /*|| StateManager.ShowState != null*/)
                FGTServiceManager.GetService<SpeedrunService>().SpeedrunState = RunState.TempDisabled;

            SetBestCameraDirector();

            var myGuy = CGM.GetNetObjectByID(CGM.GetFocusedNetId()).gameObject;
            var beh = myGuy.AddComponent<FallGuyBehaviour>();
            beh.Init();
        }

        void OnIntroEnd()
        {
            return;

            CGM.FinishPreparationPhase();
            StateManager.FGTCurrentState = FGTStateManager.FGTState.IntroComplete;
            if (CGM.GameRules.TeamCount > 0 && CGM.GameRules.IsTeamGameMode == true)
                Resources.FindObjectsOfTypeAll<GameplayScoringViewModel>().FirstOrDefault().InitialiseScoring();
            RoundCamera?.SnapCameraNextFrame();
            RoundCamera.ForceRecenterToHeading();
            RoundCamera.AddCloseCameraTarget(FGBehaviour.FallGuy, true);
            FGBehaviour.FGCC.SpeedBoostManager.SetAuthority(true);
            Resources.FindObjectsOfTypeAll<XRayMeshRendererTracker>().FirstOrDefault().AddXRayControllerForCharacter(FGBehaviour.FGCC);
            if (CameraDistance.Value > 0)
            {
                foreach (FallGuysCameraAvoidence camFov in Resources.FindObjectsOfTypeAll<FallGuysCameraAvoidence>())
                    camFov._maxDistance = CameraDistance.Value;
            }
            if (CGM.GameRules.IsTimeAttackGameMode && !FGTServiceManager.GetService<RoundOptionsService>().ReturnLatestOptions().TimeLimit)
            {
                foreach (GameplayTimerViewModel timer in Resources.FindObjectsOfTypeAll<GameplayTimerViewModel>().ToList().FindAll(x => x.gameObject.scene.name == "DontDestroyOnLoad"))
                {
                    timer._shouldShowTimeAnim = false;
                    timer._shouldShowTimeRemaining = false;
                }
            }

            StateManager.GameLoading.HandleGameServerStartGame(new(0, CGM.CurrentGameSession.EndRoundTime, 0, 1, CGM.GameRules.NumPerVsGroup, 1, 0));
            FGTLog(LogLevel.Info, GetType(), "Intro complete");
        }


        public override void UpdateService()
        {
            UpdateRateLimit();       
        }

        string ThrowRateLimitText()
        {
            string outLine = $"{LocalizedStr("fgc_temp_request_ban")}\n\n{LocalizedStr("fgc_temp_request_ban_desc")}";

            if (RateLimitedTimes == 3)
                outLine = $"{LocalizedStr("fgc_temp_request_ban_1")}\n\n{LocalizedStr("fgc_temp_request_ban_desc")}";

            else if (RateLimitedTimes == 4)
                outLine = $"{LocalizedStr("fgc_temp_request_ban_2")}\n\n{LocalizedStr("fgc_temp_request_ban_desc")}";

            if (RateLimitedTimes < 5)
                return outLine + $": {FGCRateLimitUntil}";
            else
                return $"{LocalizedStr("fgc_temp_request_ban_3")}";
        }

        void UpdateRateLimit()
        {
            if (DateTime.Now >= NextRateLimitDeinc && CurrRateLimit > 0 && !FGCRateLimit)
            {
                CurrRateLimit--;
                NextRateLimitDeinc = DateTime.Now.AddSeconds(15);
            }

            else if (FGCRateLimit && DateTime.Now >= FGCRateLimitUntil)
            {
                CurrRateLimit = 0;
                FGCRateLimitUntil = DateTime.MinValue;
            }
        }

        public void IncreaseRateLimit()
        {
            NextRateLimitDeinc = DateTime.Now.AddSeconds(15);
            CurrRateLimit++;
            if (CurrRateLimit >=  RateLimitTarget )
            {
                RateLimitedTimes++;
                FGCRateLimitUntil = DateTime.Now.AddMinutes(1);
                if (RateLimitedTimes >= 6)
                    Application.Quit();
            }
        }

        public void LoadLatestRound(LoadSceneMode mode)
        {
            if (!string.IsNullOrEmpty(RoundToLoad) && RoundToLoad != "round_")
                LoadCMSRound(RoundToLoad, mode);
            else
                ErrorPopup(LocalizedStr("gui_default_load"));

            UsingAdditiveLoad = mode == LoadSceneMode.Additive;
        }

        public void HideMenus()
        {
            try { AudioMixing.Instance.ResetAllSnapshotParams(); } catch { }

            if (SpeedrunMode.Value)
                FGTServiceManager.GetService<SpeedrunService>().HandleState(RunState.Inactive);

            var menu = StateManager.GetState<MenuState>();

            if (menu != null && menu.menuManager != null)
            {
                menu.menuManager.StopMusic(true);
                try
                {
                    menu.menuManager.HideLobbyScreen();
                    menu.menuManager.HideChallenges();
                    menu.menuManager.RemoveMainMenuBuilder();
                }
                catch { }
            }
        }

        public void LoadCMSRound(string roundToLoad, LoadSceneMode mode)
        {
            if (!StateManager.RoundLoadingAllowed)
            {
                ErrorPopup(LocalizedStr("gui_rl_block"));
                return;
            }

            StateManager.RoundLoadingAllowed = false;

            try
            {
                StateManager.SetNewRound(CMSLoader.Instance.CMSData.Rounds[roundToLoad]);

                StateManager.ShowState?.IsRoundValid(roundToLoad);

                if (StateManager.CurrentRound == null)
                {
                    StateManager.RoundLoadingAllowed = true;
                    ErrorPopup($"{LocalizedStr("unable_to_find_round", [roundToLoad])}");
                    return;
                }

                if (!StateManager.BuildScenes[FGTStateManager.AssemblyHash].Contains(StateManager.CurrentRound.GetSceneName()))
                {
                    StateManager.RoundLoadingAllowed = true;
                    ErrorPopup($"{LocalizedStr("unable_to_find_round_scene", [StateManager.CurrentRound.GetSceneName()])}");
                    return;
                }

                if (StateManager.IsInEditor && mode == LoadSceneMode.Single)
                {
                    StateManager.RoundLoadingAllowed = true;
                    ErrorPopup($"{LocalizedStr("fgc_desc")}");
                    return;
                }

                if (StateManager.CurrentRound.SceneData == null)
                {
                    StateManager.RoundLoadingAllowed = true;
                    ErrorPopup($"{LocalizedStr("null_secene")}");
                    return;
                }

                InternalState.ResetRandomCosmetics();
                FGTLog(LogLevel.Message, base.GetType(), "[LOAD ACTION] Trying to load: " + StateManager.CurrentRound.Id);
                FMODTool.UnloadAllLoadedBanks(FMODTool.UnloadParam.Default);
                StateManager.IsFGC = StateManager.CurrentRound.IsUGC();


                StateManager.HandleFGState(PlayerState.Despawned);
                StateManager.CurrentRound.GameRules.ShowQualificationProgressUI = StateManager.CurrentRound.Archetype.Id.Contains("race");

                if (StateManager.CurrentRound.Archetype.Id == "archetype_invisibeans")
                    StateManager.CurrentRound.Archetype.TagColour = "#5cedeb";

                StateManager.CurrentRound.GameRules.TimerVisibilityThreshold = 9999;
                NetworkGameData.ClearCurrentGameOptions();
                isXtremeRound = StateManager.CurrentRound.Id.Contains("xtreme");


                if (RandomMusic.Value)
                {
                    StateManager.CurrentRound.IngameMusicEvent = null;
                    StateManager.CurrentRound.IngameMusicSoundBank = null;
                }

                if (mode == LoadSceneMode.Additive)
                {
                    StateManager.RoundLoadingAllowed = true;
                    Addressables.LoadScene(StateManager.CurrentRound.GetSceneName(), LoadSceneMode.Additive);
                }
                else
                {
                    Resources.FindObjectsOfTypeAll<UICanvas>().FirstOrDefault().RemoveAllScreens();
                    StateManager.HandleFGTState(FGTStateManager.FGTState.RoundLoading);

                    if (!StateManager.CurrentRound.IsUGC())
                    {
                        var stats = FGTServiceManager.GetService<StatisticsService>();

                        //HideMenus();
                        //SetupForRound(StateManager.CurrentRound);

                        StateManager.DropActiveState();
                        //StateManager.GameLoading = new StateGameLoading(GlobalGameStateClient.Instance._gameStateMachine, GlobalGameStateClient.Instance.CreateClientGameStateData(), GamePermission.Player, false, false);
                        //GlobalGameStateClient.Instance._gameStateMachine.ReplaceCurrentState(StateManager.GameLoading.Cast<IGameState>());

                        FGTServiceManager.GetService<LocalServerService>().SingleplayerGame(StateManager.CurrentRound);

                        stats.ProcessNewRound(StatisticsService.RoundResult.NewRound);
                        stats.SetNewRound(StateManager.CurrentRound);

                    }
                    else
                        LoadFGCRound(StateManager.CurrentRound.SceneData.DlcLevel.ShareCode, null, true);
                }
            }
            catch (Exception e)
            {
                StateManager.RoundLoadingAllowed = true;
                ErrorPopup(e, displayOnlyError: false, forceLeaveToMenu: true);
            }
        }

        void SetupForRound(Round round)
        {
            var options = FGTServiceManager.GetService<RoundOptionsService>().ReturnLatestOptions();

            NetworkGameData.ClearCurrentGameOptions();
            NetworkGameData.SetGameOptionsFromRoundData(StateManager.CurrentRound);

            var newOptions = NetworkGameData.currentGameOptions_;

            if (options.RoundSeed != 0)
            {
                newOptions._randomSeed = options.RoundSeed;
                newOptions._directorSeed = options.RoundSeed;
            }
            else
            {
                var randSeed = Random.Range(0, int.MaxValue);
                newOptions._randomSeed = randSeed;
                newOptions._directorSeed = randSeed;
            }

            NetworkGameData.currentGameOptions_ = newOptions;
            NetworkGameData.SetInitialRoundPlayerCount(1);

        }

        public LevelInfoDto preloadedDTO;
        public void GetOnlyLevelDto(string code)
        {
            if (!FGCRateLimit)
            {
                IncreaseRateLimit();
                FraggleCommonManager.Instance.FraggleLevelRepository.RequestFraggleLevelData(new(code, new Il2CppSystem.Nullable<int>(0)), new Action<FraggleLevelData>((FraggleLevelData data) =>
                {
                    preloadedDTO = (data != null) ? data.LevelInfoDto : null;
                    FGToolsUI.NewGUI.Instance.levelInfo.text = FGToolsUI.NewGUI.ParseLevelDTO(preloadedDTO);
                    if (data.LevelInfoDto != null)
                        FGTServiceManager.GetService<StatisticsService>().AddFGCHistoryRound(code);
                }), null);
            }
            else
                FGToolsUI.NewGUI.Instance.levelInfo.text = $"{ThrowRateLimitText()}";
        }

        public void LoadFGCRound(string code, LevelInfoDto preloadedDto, bool userRequest)
        {
            if (StateManager.IsInEditor)
            {
                StateManager.RoundLoadingAllowed = true;
                ErrorPopup($"{LocalizedStr("fgc_desc")}");
                return;
            }

            if (StateManager.ExploreState != null && userRequest)
                return;

            if (code.Length > 14)
            {
                ErrorPopup(LocalizedStr("fgc_length") + $" {code.Length}");
                return;
            }

            if (!StateManager.RoundLoadingAllowed)
            {
                return;
            }

            if (FGCRateLimit)
            {
                DoModal(LocalizedStr("ratelimited_title"), $"{LocalizedStr("fgc_temp_request_ban")}: {FGCRateLimitUntil}", UIModalMessage.ModalType.MT_OK, UIModalMessage.OKButtonType.Default, hideGUI: ModalHideGUIType.KeepHiddenForThisModal);
                return;
            }

            StateManager.RoundLoadingAllowed = false;

            try
            {
                var gp = StateManager.GetState<GameplayState>();
                gp?.FinishGameplay();

                LevelInfoDto lvl = null;
                Round templateRound = null;

                Action<LevelInfoDto> Load = new((LevelInfoDto preloadedDto) => {
                    if (preloadedDto != null)
                        lvl = preloadedDto;

                    string template = "fraggle_gamemode_gauntlet_template";
                    if (lvl._gameModeId.Contains("SURVIVAL"))
                        template = "fraggle_gamemode_survival_template";
                    if (lvl._gameModeId.Contains("POINTS"))
                        template = "fraggle_gamemode_points_template";

                    templateRound = CMSLoader.Instance.CMSData.Rounds[template];

                    if (templateRound == null)
                    {
                        UIManager.Instance.HideScreen<LoadingSpinnerScreenViewModel>();
                        StateManager.RoundLoadingAllowed = true;
                        ErrorPopup($"{LocalizedStr("fgc_error")}\n\n{"fgc_missing_template"}\n{LocalizedStr("gui_error_msg")} {LocalizedStr("gui_error_1")}");
                        return;
                    }

                    var fgcFound = Round.CreateInstanceForUGC(lvl, templateRound);
                    fgcFound.GameRules.IsFinalRound = false;
                    fgcFound.ExploreOverrideTemplateId = template;
                    if (!fgcFound.Archetype.Id.Contains("race"))
                        fgcFound.GameRules.ShowQualificationProgressUI = false;

                    //HideMenus();
                    StateManager.SetNewRound(fgcFound);
                    SetupForRound(fgcFound);
                    if (fgcFound.SceneData.DlcLevel.ShareCode != null)
                    {
                        UIManager.Instance.HideScreen<LoadingSpinnerScreenViewModel>();
                        StateManager.RoundLoadingAllowed = true;
                        if (StateManager.IsPlayingExploreFGC)
                            FGTServiceManager.GetService<StatisticsService>().AddFGCHistoryRound(code);
                        //StateManager.GameLoading = new StateGameLoading(GlobalGameStateClient.Instance._gameStateMachine, GlobalGameStateClient.Instance.CreateClientGameStateData(), GamePermission.Player, false, false);
                        FGTServiceManager.GetService<LocalServerService>().SingleplayerGame(StateManager.CurrentRound);
                        //GlobalGameStateClient.Instance._gameStateMachine.ReplaceCurrentState(StateManager.GameLoading.Cast<IGameState>());
                        FGTServiceManager.GetService<StatisticsService>().SetNewRound(fgcFound);
                    }
                    else
                    {
                        UIManager.Instance.HideScreen<LoadingSpinnerScreenViewModel>();
                        StateManager.RoundLoadingAllowed = true;
                        ErrorPopup(LocalizedStr("bad_fgc_level"));
                    }
                });

                if (preloadedDto != null)
                {
                    Load.Invoke(preloadedDto);
                    return;
                }

                UIManager.Instance.ShowScreen<LoadingSpinnerScreenViewModel>(new() { UseScrim = true });
                IncreaseRateLimit();
                FraggleCommonManager.Instance.FraggleLevelRepository.RequestFraggleLevelData(new(code, new Il2CppSystem.Nullable<int>(0)), new Action<FraggleLevelData>((FraggleLevelData data) => {
                    StateManager.IsFGC = true;
                    FraggleCommonManager.Instance.IsInLevelEditor = false;
                    FraggleCommonManager.Instance.SetModeToBuild(new());
                    lvl = data != null ? data.LevelInfoDto : LevelInfoDto.CreatePlaceholderData();
                    Load.Invoke(preloadedDto);
                }), null);
            }
            catch (Exception ex)
            {
                UIManager.Instance.HideScreen<LoadingSpinnerScreenViewModel>();
                StateManager.RoundLoadingAllowed = true;
                ErrorPopup(ex);
            }
        }

        public HashSet<string> GetFiltredRounds(List<Round> CmsRounds)
        {
            var PossibleRoundsOverride = new HashSet<string>();
            foreach (var possibleRound in CmsRounds)
            {
                try
                {
                    switch (RoundsFilter.Value)
                    {
                        case RandomRoundsFilter.Race:
                            if (possibleRound.Archetype.Id == "archetype_race")
                                PossibleRoundsOverride.Add(possibleRound.Id);
                            break;
                        case RandomRoundsFilter.Logic:
                            if (possibleRound.Archetype.Id == "archetype_logic")
                                PossibleRoundsOverride.Add(possibleRound.Id);
                            break;
                        case RandomRoundsFilter.Survival:
                            if (possibleRound.Archetype.Id == "archetype_survival")
                                PossibleRoundsOverride.Add(possibleRound.Id);
                            break;
                        case RandomRoundsFilter.Final:
                            if (possibleRound.Archetype.Id == "archetype_final")
                                PossibleRoundsOverride.Add(possibleRound.Id);
                            break;
                        case RandomRoundsFilter.Team:
                            if (possibleRound.Archetype.Id == "archetype_team")
                                PossibleRoundsOverride.Add(possibleRound.Id);
                            break;
                        case RandomRoundsFilter.TimeAttack:
                            if (possibleRound.Archetype.Id == "archetype_timeattack")
                                PossibleRoundsOverride.Add(possibleRound.Id);
                            break;
                        case RandomRoundsFilter.Hunt:
                            if (possibleRound.Archetype.Id == "archetype_hunt")
                                PossibleRoundsOverride.Add(possibleRound.Id);
                            break;
                        default:
                            PossibleRoundsOverride.Add(possibleRound.Id);
                            break;
                    }
                }
                catch
                {
                    return null;
                }
            }
            return PossibleRoundsOverride;
        }

        List<string> RealRoundList = [];
        public void SetupCMSRoundList()
        {
            int stat = 0;
            RealRoundList.Clear();
            foreach (var round in CMSLoader.Instance._roundsSO.Rounds.Values)
            {
                if (StateManager.BuildScenes[FGTStateManager.AssemblyHash].Contains(round.GetSceneName()) && !round.IsUGC())
                {
                    RealRoundList.Add(round.Id);
                }
                else
                    stat++;
            }

            FGTLog(LogLevel.Info, base.GetType(), $"{stat} rounds were excluded from random round pool.");
        }

        public void LoadRandomCms(HashSet<string> customList = null)
        {
            if (StateManager.IsPlayingExploreFGC)
            {
                StateManager.ExploreState.RequestNewRound();
                return;
            }

            List<Round> RoundsResult = [];
            if (customList == null)
            {
                foreach (var CmsRounds in RealRoundList)
                {
                    var actualRound = CMSLoader.Instance.CMSData.Rounds[CmsRounds];
                    RoundsResult.Add(actualRound);
                }
            }
            else
            {
                foreach (string str in customList)
                {
                    if (RealRoundList.Contains(str))
                        RoundsResult.Add(CMSLoader.Instance._roundsSO.Rounds[str]);
                }
            }

            var target = GetFiltredRounds(RoundsResult);
            if (target != null && target.Count > 0)
            {
                var output = target.ElementAt(UnityEngine.Random.Range(0, target.Count));
                LoadCMSRound(output, LoadSceneMode.Single);
            }
            else
                ErrorPopup(LocalizedStr("gui_random_no_rounds"));
        }

        void SetBestCameraDirector()
        {
            if (!StateManager.IsFGC)
            {
                foreach (wle.LevelEditorCameraAvoidance C in Resources.FindObjectsOfTypeAll<wle.LevelEditorCameraAvoidance>())
                    UnityEngine.Object.Destroy(C.gameObject);
            }

            foreach (CameraDirector cam in Resources.FindObjectsOfTypeAll<CameraDirector>())
            {
                if (!cam.gameObject.activeSelf)
                    UnityEngine.Object.Destroy(cam.gameObject);
            }

            RoundCamera = CGM.CameraDirector;

            FGTLog(LogLevel.Info, base.GetType(), $"Round camera is now points to {RoundCamera.gameObject.name}");
        }

        [HideFromIl2Cpp]
        public IEnumerator PrepareGame(string levelHash)
        {
            FGTLog(LogLevel.Info, base.GetType(), $"Starting preparation for round {StateManager.CurrentRound.Id} | Seed {GlobalGameStateClient.Instance.GameStateView.RoundRandomSeed}");
            lastPrepStart = DateTime.UtcNow;

            StateManager.HandleFGTState(FGTStateManager.FGTState.SceneLoaded);

            yield return new WaitForEndOfFrame();

            if (StateManager.PiratedGame)
            {
                yield break;
            }

            CGM.GameRules.PreparePlayerStartingPositions(1);

            if (CGM._round.Archetype.Id.Contains("attack"))
                FGTServiceManager.GetService<SpeedrunService>().SpeedrunState = RunState.TimeAttack;
            else if (StateManager.ExploreState != null /*|| StateManager.ShowState != null*/)
                FGTServiceManager.GetService<SpeedrunService>().SpeedrunState = RunState.TempDisabled;
            SetBestCameraDirector();

            yield return new WaitForEndOfFrame();

            SpawnFallGuy();

            if (CGM.GameRules != null)
            {
                if (PointsObjective.Value != 0 && CGM.GameRules.ScoreDisplayMode != ScoreDisplayModes.Percentage)
                    CGM.GameRules.ScoreTarget = PointsObjective.Value;

                else if (CGM.GameRules.ScoreDisplayMode == ScoreDisplayModes.Percentage && CGM._round.Id.Contains("air"))
                    CGM.GameRules.ScoreTarget = AirTimeObjective.Value;

                if (customObjectiveText.Value)
                {
                    AddCMSString("custom_objective", objectiveText.Value);
                    CGM.GameRules.ObjectiveText = "custom_objective";
                }
            }


            yield return new WaitForSeconds(2f);

            if (FGBehaviour != null)
            {
                FGBehaviour.Init();

                var target = FGTServiceManager.GetService<RoundOptionsService>().ReturnLatestOptions().TimeLimitLength;

                //if (FGBehaviour.AllowRoundEnd && !FGBehaviour.OverrideRoundEnd)
                //{
                //    StateManager.CGM.CurrentGameSession._gameCountdownTimerVisibilityTime = 99999;
                //    if (target > 0)
                //    {
                //        StateManager.CGM.CurrentGameSession._endRoundTime = target;
                //        StateManager.CGM.CurrentGameSession._defaultRoundLength = target;
                //    }
                //}
                //else if (FGBehaviour.OverrideRoundEnd)
                //{
                //    StateManager.CGM.CurrentGameSession._gameCountdownTimerVisibilityTime = 99999;
                //}
                //else
                //{
                //    StateManager.CGM.CurrentGameSession._gameCountdownTimerVisibilityTime = -1;
                //}

                yield return new WaitForSeconds(0.05f);
                HideLoadingScreens();
                StateManager.GameLoading.OnServerRequestStartIntroCameras();
            }
            else
                FGTLog(LogLevel.Error, base.GetType(), "Unable to finish loading, fall guy failed to spawn.");


            //FMODTool.PushBanks(StateManager.CGM._levelSoundBanks);

            StateManager.ForceSetState(new GameplayState());

            FGTLog(LogLevel.Info, base.GetType(), $"Finished preparation for round {StateManager.CurrentRound.Id}. It took  {DateTime.UtcNow.Subtract(lastPrepStart).TotalSeconds:F3}sec");
            GC.Collect();
        }

        public void HideLoadingScreens()
        {
            if (!StateManager.IsFGC && !StateManager.IsPlayingExploreFGC)
                UIManager.Instance.HideScreen<LoadingGameScreenViewModel>(ScreenStackType.LoadingScreen);

            if (StateManager.IsPlayingExploreFGC)
                UIManager.Instance.HideScreen<LoadingUPGameScreenViewModel>(ScreenStackType.LoadingScreen);
            else
                UIManager.Instance.HideScreen<LoadingUGCGameScreenViewModel>(ScreenStackType.LoadingScreen);

        }

        public void GenerateCMSList()
        {
            List<string> cmsRounds = [];
            int lineNumber = 1;

            foreach (RoundsSO roundsSO in Resources.FindObjectsOfTypeAll<RoundsSO>())
            {
                foreach (var pair in roundsSO.Rounds)
                {
                    Round cmsData = pair.Value;

                    if (cmsData != null && !cmsData.IsUGC())
                    {
                        string roundName = cmsData.DisplayName != null && cmsData.DisplayName != "ЛЫЖЕПАД" ? cmsData.DisplayName : cmsData.DisplayName + " КСТА";
                        string levelType = cmsData.Archetype != null && cmsData.Archetype.Name != null ? cmsData.Archetype.Name : "(EMPTY)";
                        string scene = (cmsData.SceneData != null && cmsData.SceneData.PrimeLevel != null && cmsData.SceneData.PrimeLevel.SceneName != null) ? cmsData.SceneData.PrimeLevel.SceneName : "(EMPTY)";

                        string cleanName = CleanStr(roundName);
                        cmsRounds.Add($"{lineNumber}. {cleanName} ({levelType.ToUpper()}) | {scene} | {pair.Key}");
                        lineNumber++;
                    }
                }

                if (!File.Exists(Plugin.CMSRounds))
                {
                    int linesCount = cmsRounds.Count;
                    File.WriteAllLines(Plugin.CMSRounds, cmsRounds);

                    File.AppendAllText(Plugin.CMSRounds, $"\nTotal Rounds Added: {linesCount} | File Generated On: {Application.version} | CMS Version: {CMSLoader.Instance.CMSData.ContentVersion}");
                }

                FGTLog(LogLevel.Message, base.GetType(), File.ReadAllText(Plugin.CMSRounds));
            }
        }
        private void SpawnFallGuy()
        {
            if (StateManager.FGTCurrentState > FGTStateManager.FGTState.RoundIntro)
            {
                ErrorPopup($"{LocalizedStr("unable_to_spawn")}\n\nFGTState: {StateManager.FGTCurrentState} FGState: {StateManager.FGCurrentState}\n\n{LocalizedStr("gui_error_msg")} {LocalizedStr("gui_error_1")}", forceLeaveToMenu: true);
                return;
            }
            try
            {
                int playerTeam = UnityEngine.Random.Range(0, CGM.GameRules.NumTeamsWanted());
                MultiplayerStartingPosition randPos;

                StateManager.HandleFGTState(FGTStateManager.FGTState.RoundIntro);

                FallGuysCharacterController fgobj = Resources.FindObjectsOfTypeAll<FallGuysCharacterController>().ToList().Find(x => x.gameObject.name == "FallGuy");
                if (fgobj == null)
                {
                    ErrorPopup($"{LocalizedStr("unable_to_spawn")}\n\nFall Guy object can't be found.\n\n{LocalizedStr("gui_error_msg")} {LocalizedStr("gui_error_1")}", forceLeaveToMenu: true);
                    return;
                }

                if (!StateManager.PiratedGame)
                    randPos = CGM.GameRules.PickStartingPosition(FallGuyBehaviour.PeakId, 0, playerTeam, 0, false);
                else
                    randPos = Resources.FindObjectsOfTypeAll<MultiplayerStartingPosition>().FirstOrDefault();

                FGTServiceManager.GetService<SpeedrunService>().SetSpawnPos(randPos.transform.position, randPos.transform.rotation);
                fgobj.transform.SetPositionAndRotation(randPos.transform.position, randPos.transform.rotation);
                fgobj.GetComponent<MotorAgent>()._motorFunctionsConfig = MotorAgent.MotorAgentConfiguration.Offline;

                GameMessageFactory.Initialize(true);

                var msg = GameMessageFactory.AllocateMessage<GameMessageServerSpawnObject>();
                msg._netObjectSpawnData = new()
                {
                    NetID = new(FallGuyBehaviour.PeakId),
                    _creationMode = NetObjectCreationMode.Spawn,
                    _lodControllerBehaviour = FG.Common.LODs.LodController.LodControllerBehaviour.Default,
                    Position = randPos.transform.position,
                    Rotation = randPos.transform.rotation,
                    Scale = Vector3.one,
                    _spawnObjectType = EnumSpawnObjectType.PLAYER,
                    _prefabHash = -491682846,
                    AdditionalSpawnData = new PlayerSpawnData()
                    {
                        _customisationSelections = GlobalGameStateClient.Instance.PlayerProfile.CustomisationSelections,
                        _accessoryEnabled = false,
                        _accountId = GlobalGameStateClient.Instance.GetLocalClientAccountID(),
                        _partyId = "",
                        _platformAccountName = GlobalGameStateClient.Instance.GetLocalPlayerName(),
                        _platformId = PlatformServices.GetPlatformDevice(),
                        _playerGeneratedName = "",
                        _playerId = FallGuyBehaviour.PeakId,
                        _playerNetworkId = GlobalGameStateClient.Instance.GetLocalClientNetworkID(),
                        _squadId = 0,
                        _teamId = -1,
                        _vsGroupId = 0,
                    }
                };

                var fallGuy = GlobalGameStateClient.Instance.NetObjectManager.SpawnNetObject(msg);
                var fgb = fallGuy.AddComponent<FallGuyBehaviour>();


                if (CGM.GameRules.IsTeamGameMode && CGM.GameRules.TeamCount > 0)
                {
                    fgb.PlayerTeamId = Random.Range(0, CGM.GameRules.NumTeamsWanted());
                    CGM.CreateTeams(CGM.GameRules.NumTeamsWanted());
                }

                if (fgb.PlayerTeamId != -1)
                    fgb.UpdateTeam(playerTeam, true);

                return;

                var fg = UnityEngine.Object.Instantiate(fgobj.gameObject);
                var fgcc = fg.GetComponent<FallGuysCharacterController>();
                var fginput = fg.GetComponent<FallGuysCharacterControllerInput>();

                fg.name = "FallGuy";
                fg.tag = "Player";

                var mpgfg = fg.AddComponent<MPGNetObject>();
                mpgfg.NetID = new MPGNetID(FallGuyBehaviour.PeakId);
                mpgfg.FGCharacterController = fgcc;
                mpgfg.IsFallGuy = true;
                mpgfg.pNetTX_ = new MPGNetTransform(mpgfg, null, null, null, false, 0);
                mpgfg.SpawnObjectType = EnumSpawnObjectType.PLAYER;

                fgcc.IsControlledLocally = true;
                fgcc.IsLocalPlayer = true;
                fgcc._pNetObject = mpgfg;
                fgcc._mpgNetObjectManager = CGM._netObjectManager;

                //var fgb = fg.AddComponent<FallGuyBehaviour>();

                var charData = fgcc.MotorAgent.CharacterData;
                if (OldPhysics.Value)
                {
                    charData.getUpJumpInterruptTime = 0.2f;
                    charData.getUpRollOverAngleThreshold = 30f;
                    charData.getUpRollOverMaxDuration = 0.5f;
                    charData.getUpRollOverRotationSpeed = 0.4f;
                    charData.getUpStandUprightAngleThreshold = 15f;
                    charData.getUpStandUprightRotationSpeed = 3f;
                    charData.impactAlongFloorMultiplier = 0.75f;
                    charData.impactOwnVelocityContribution = 0.8f;
                    charData.impactVerticalMultiplier = 0.45f;
                    charData.ragdollRepinMaxDelay = 0.5f;
                    charData.ragdollRepinSpeed = 0.66f;
                    charData.rollingInAirMaxSpeed = 0.2f;
                    charData.stunnedMovementDelay = 0.2f;
                    charData.jumpForceUltimateParty = charData.jumpForce;
                    charData.aerialTurnSpeedUltimateParty = charData.aerialTurnSpeed;
                    charData.forwardAccCurveUltimateParty = charData.forwardAccCurveUltimateParty;
                }
                charData.divePlayerSensitivity = DiveSens.Value;
                fginput.SetPlayerIndex(0);

                CGM.SetupPlayerUpdateManagerAndRegister(fgcc, true);
                RewiredManager.Instance.SetActiveMap(0, 0, false, false);

                CGM._clientPlayerManager.ClearAllPlayers();

                if (CGM.GameRules.IsTeamGameMode && CGM.GameRules.TeamCount > 0)
                {
                    fgb.PlayerTeamId = Random.Range(0, CGM.GameRules.NumTeamsWanted());
                    CGM.CreateTeams(CGM.GameRules.NumTeamsWanted());
                }
                CGM.HandlePlayerBeingSpawned(fgcc.NetObject, FallGuyBehaviour.PeakId, GlobalGameStateClient.Instance.GetLocalClientNetworkID(), GlobalGameStateClient.Instance.GetLocalClientAccountID(), ClientBuildDetails.Platform, GlobalGameStateClient.Instance.PlayerProfile.PlatformAccountName, GlobalGameStateClient.Instance.PlayerProfile.PlatformAccountName, 0, fgb.PlayerTeamId, "0", 0, true, GlobalGameStateClient.Instance.PlayerProfile.CustomisationSelections);

                if (fgb.PlayerTeamId != -1)
                    fgb.UpdateTeam(playerTeam, true);

                FGTLog(LogLevel.Info, base.GetType(), "Succeed spawn");
            }
            catch (Exception e)
            {
                ErrorPopup(e, forceLeaveToMenu: true, displayOnlyError: false);
            }
        }

        public override void DrawGUI()
        {

        }

        public void RefreshUI()
        {
            RefreshRoundsInfo();
            PopulateRounds();
        }
    }
}
