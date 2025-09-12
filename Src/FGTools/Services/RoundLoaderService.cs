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
        List<string> RealRoundList = [];
        public override void RegisterService()
        {
            GameActions.OnIntroStarts += OnIntroStart;
            GameActions.OnIntroEnds += OnIntroEnd;
        }

        void OnIntroEnd()
        {
            StateManager.HandleFGTState(ToolsState.IntroComplete);
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

        void OnIntroStart()
        {
            StateManager.ForceSetState(new GameplayState());
            StateManager.HandleFGTState(ToolsState.RoundIntro);

            if (CGM._round.Archetype.Id.Contains("attack"))
                FGTServiceManager.GetService<SpeedrunService>().SpeedrunState = RunState.TimeAttack;
            else if (StateManager.ExploreState != null /*|| StateManager.ShowState != null*/)
                FGTServiceManager.GetService<SpeedrunService>().SpeedrunState = RunState.TempDisabled;

            SetBestCameraDirector();

            var myGuy = CGM.GetNetObjectByID(CGM.GetFocusedNetId()).gameObject;
            var beh = myGuy.AddComponent<FallGuyBehaviour>();
            beh.Init();
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
        public void LoadCMSRound(string roundToLoad, LoadSceneMode mode)
        {
            if (!StateManager.RoundLoadingAllowed)
            {
                ErrorPopup(LocalizedStr("gui_rl_block"));
                return;
            }

            if (!CMSLoader.Instance.CMSData.Rounds.ContainsKey(roundToLoad))
            {
                ErrorPopup(LocalizedStr("gui_missing_round", [roundToLoad]));
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
                FMODTool.UnloadAllLoadedBanks();


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
                    StateManager.HandleFGTState(FGTStateManager.ToolsState.RoundLoading);

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
            if (string.IsNullOrEmpty(code))
            {
                ErrorPopup(LocalizedStr("gui_default_load"));
                return;
            }

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
                DoModal(new(LocalizedStr("ratelimited_title"), $"{LocalizedStr("fgc_temp_request_ban")}: {FGCRateLimitUntil}", UIModalMessage.ModalType.MT_OK, UIModalMessage.OKButtonType.Default, hideLvl: ModalHideGUIType.KeepHiddenForThisModal));
                return;
            }

            StateManager.RoundLoadingAllowed = false;

            try
            {
                var gp = StateManager.GetState<GameplayState>();

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

                    StateManager.SetNewRound(fgcFound);
                    SetupForRound(fgcFound);
                    if (fgcFound.SceneData.DlcLevel.ShareCode != null)
                    {
                        UIManager.Instance.HideScreen<LoadingSpinnerScreenViewModel>();
                        StateManager.RoundLoadingAllowed = true;
                        if (StateManager.IsPlayingExplore)
                            FGTServiceManager.GetService<StatisticsService>().AddFGCHistoryRound(code);
                        FGTServiceManager.GetService<LocalServerService>().SingleplayerGame(StateManager.CurrentRound);
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
                FraggleCommonManager.Instance.FraggleLevelRepository.RequestFraggleLevelData(new(code, new Il2CppSystem.Nullable<int>(0)), new Action<FraggleLevelData>((FraggleLevelData data) => 
                {
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

        public void SetupCMSRoundList()
        {
            int stat = 0;
            RealRoundList.Clear();

            foreach (var round in CMSLoader.Instance._roundsSO.Rounds.Values)
            {
                if (StateManager.BuildScenes[FGTStateManager.AssemblyHash].Contains(round.GetSceneName()) && !round.IsUGC())
                    RealRoundList.Add(round.Id);
                else
                    stat++;
            }

            FGTLog(LogLevel.Info, base.GetType(), $"{stat} rounds were excluded from random round pool.");
        }

        public void LoadRandomCms(HashSet<string> customList = null)
        {
            if (StateManager.IsPlayingExplore && StateManager.ExploreState.CurrentJoinPolicy != UltimatePartyState.JoinPolicy.Random)
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

        public void HideLoadingScreens()
        {
            if (!StateManager.IsFGC && !StateManager.IsPlayingExplore)
                UIManager.Instance.HideScreen<LoadingGameScreenViewModel>(ScreenStackType.LoadingScreen);

            if (StateManager.IsPlayingExplore)
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

                if (!File.Exists(Launcher.CMSRounds))
                {
                    int linesCount = cmsRounds.Count;
                    File.WriteAllLines(Launcher.CMSRounds, cmsRounds);

                    File.AppendAllText(Launcher.CMSRounds, $"\nTotal Rounds Added: {linesCount} | File Generated On: {Application.version} | CMS Version: {CMSLoader.Instance.CMSData.ContentVersion}");
                }

                FGTLog(LogLevel.Message, base.GetType(), File.ReadAllText(Launcher.CMSRounds));
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
