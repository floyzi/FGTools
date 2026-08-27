using BepInEx.Logging;
using FG.Common.CMS;
using FGTools.Services.Logic;
using FGTools.UI;
using FGTools.UI.Tabs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UniverseLib.UI.Models;
using static FGTools.Internal.Extensions.FLZ_Extensions;
using static FGTools.Services.LocalizationService;
using static FGTools.Services.MenuThemeService;
using static FGTools.Services.StatisticsService;
using static FGTools.States.Logic.FGTStateManager;
using static FGTools.UI.FGToolsUI;
namespace FGTools.Services
{
    internal class StatisticsService : FGTService, IFGTGUIHelper
    {
        public class StatJSON
        {
            public float TimeInGame { get; set; }
            public int TotalRoundsLoaded { get; set; }
            public int GameLaunchedTimes { get; set; }
            public int TotalAttemptsSp { get; set; }
            public float TimeInMenu { get; set; }
            public float TimeInFGC { get; set; }
            public int QualTotal { get; set; }
            public int ElimTotal { get; set; }
            public int WinTotal { get; set; }
            public int PowUsages { get; set; }
            public string StatsUser { get; set; }
            public List<string> RoundHistory { get; set; }
            public List<string> FGCSearchHistory { get; set; }
            public int CollectablePickup { get; set; }
        }

        public enum RoundResult
        {
            NewRound,
            Qual,
            Elim,
            Win,
            Leave
        }

        public StatJSON CurrentStats;
        public List<List<string>> HistoryPages = [];

        public string ProcessedRoundID = "none";
        public readonly float SaveTime = 300f;
        public float TimeElapsed = 0;
        float _roundLength;
        Round _currentRound;
        Round _previousRound;
        int _currPage;

        ButtonRef HistoryPlus;
        ButtonRef HistoryMinus;
        Text RoundHistoryTXT;
        Text DisplayInfo;
        GameObject RoundHistory;
        GameObject HistoryActions;
        ButtonRef toggleHistory;

        public override void RegisterService()
        {
            Init(false);
            CurrentStats.GameLaunchedTimes++;
        }

        public void Init(bool cleanup)
        {
            FGTLog(LogLevel.Info, GetType(), $"init: cleanup = {cleanup}");
            if (cleanup && File.Exists(Launcher.StatsFile))
            {
                File.Delete(Launcher.StatsFile);
            }
            if (File.Exists(Launcher.StatsFile))
                CurrentStats = JsonSerializer.Deserialize<StatJSON>(File.ReadAllText(Launcher.StatsFile));
            else
            {
                var newStats = new StatJSON
                {
                    TimeInGame = 0,
                    TotalRoundsLoaded = 0,
                    GameLaunchedTimes = 1,
                    TotalAttemptsSp = 0,
                    TimeInMenu = 0,
                    TimeInFGC = 0,
                    ElimTotal = 0,
                    PowUsages = 0,
                    QualTotal = 0,
                    WinTotal = 0,
                    StatsUser = "DEFAULT",
                };
                var json = JsonSerializer.Serialize(newStats);
                File.AppendAllText(Launcher.StatsFile, json);
                CurrentStats = JsonSerializer.Deserialize<StatJSON>(File.ReadAllText(Launcher.StatsFile));
            }

            RefreshHistoryPages();
        }

        void RefreshHistoryPages()
        {
            if (HistoryPages == null) return;

            HistoryPages.Clear();

            if (CurrentStats != null && CurrentStats.RoundHistory != null && CurrentStats.RoundHistory.Count > 0)
            {
                for (int i = 0; i < CurrentStats.RoundHistory.Count; i += 50)
                {
                    HistoryPages.Add(CurrentStats.RoundHistory.GetRange(i, Math.Min(50, CurrentStats.RoundHistory.Count - i)));
                }

                _currPage = HistoryPages.Count - 1;
            }
            else
            {
                HistoryActions.gameObject.SetActive(false);
                _currPage = 0;
            }

        }

        public void Save()
        {
            var stats = JsonSerializer.Serialize(CurrentStats);
            File.WriteAllText(Launcher.StatsFile, stats);
            FGTLog(LogLevel.Info, base.GetType(), "Save");
        }

        public void HistoryNavForward()
        {
            if (_currPage < HistoryPages.Count - 1)
            {
                _currPage++;
                LoadPage();
            }
        }

        public void HistoryNavBack()
        {
            if (_currPage > 0)
            {
                _currPage--;
                LoadPage();
            }
        }

        public void LoadPage()
        {
            if (HistoryPages.Count > 0)
            {
                DisplayInfo.text = $"{LocalizedStr("gui_page")} {_currPage + 1} {LocalizedStr("gui_out_of")} {HistoryPages.Count}";
                int ab = _currPage + 1 * HistoryPages[_currPage].Count;
                List<string> sortedList = new([.. HistoryPages[_currPage]]);
                sortedList.Reverse();
                RoundHistoryTXT.text = string.Join("\n", sortedList.ToArray().Select((line, index) => $"{ab -= 1}. | {line}"));
                HistoryPlus.Component.interactable = _currPage + 1 != HistoryPages.Count;
                HistoryMinus.Component.interactable = _currPage > 0;
                HistoryActions.gameObject.SetActive(FGToolsUI.Instance.CurrentTab.Tab == FGToolsUI.Tab.Misc && RoundHistory.gameObject.activeSelf);
            }
            else
                HistoryActions.gameObject.SetActive(false);
        }

        public void AddFGCHistoryRound(string code)
        {
            CurrentStats.FGCSearchHistory ??= [];
            CurrentStats.FGCSearchHistory.Add(code);

            FGToolsUI.Instance.GetTab<RoundLoaderTab>(FGToolsUI.Tab.RoundLoader).RefreshFGCHistory(code);

            Save();
        }

        public void ProcessNewRound(RoundResult result)
        {
            if (_currentRound != null)
                _previousRound = _currentRound;
       
            if (CurrentStats != null && _previousRound != null && ProcessedRoundID != _currentRound.Id)
            {
                ProcessedRoundID = _previousRound.Id;
                string resultString = "Undefined";
                switch (result)
                {
                    case RoundResult.NewRound:
                        resultString = $"<color=#9eb6de>{LocalizedStr("stats_result_new")}</color>";
                        break;
                    case RoundResult.Leave:
                        resultString = $"<color=#dea09e>{LocalizedStr("stats_result_leave")}</color>";
                        break;
                    case RoundResult.Win:
                        resultString = $"<color=#e6de97>{LocalizedStr("gui_run_win")}</color>";
                        break;
                    case RoundResult.Qual:
                        resultString = $"<color=#88dee3>{LocalizedStr("gui_run_qual")}</color>";
                        break;
                    case RoundResult.Elim:
                        resultString = $"<color=#e388d5>{LocalizedStr("gui_run_elim")}</color>";
                        break;

                }

                string outstr = $"{CleanStr(_previousRound.DisplayName.Text)} (<color={_previousRound.Archetype.TagColour}>{_previousRound.Archetype.Name.ToUpper()}</color>) | {LocalizedStr("gui_date")}: <color=grey>{DateTime.Now}</color> | {LocalizedStr("round_length")}: <color=grey>{TimeSpan.FromSeconds(_roundLength):mm':'ss}</color> | {LocalizedStr("gui_result")}: {resultString}";
                
                CurrentStats.RoundHistory ??= [];
                CurrentStats.RoundHistory.Add(outstr);

                RefreshHistoryPages();
                Save();
            }
        }

        public void SetNewRound(Round newRound)
        {
            _currentRound = newRound;
            //processedRoundID = newRound.Id;
        }
        public void ResetTimer() => _roundLength = 0;
        public void ValidateStats(string username)
        {
            var user = CurrentStats.StatsUser;
            if (user != null && user != "DEFAULT" && user != username)
                Init(true);
            else
                CurrentStats.StatsUser = username;
        }

        public override void UpdateService()
        {
            if (CurrentStats.RoundHistory == null || CurrentStats.RoundHistory.Count == 0)
                RoundHistoryTXT.text = LocalizedStr("gui_nothing2see");

            if (CurrentStats != null)
            {
                CurrentStats.TimeInGame += Time.unscaledDeltaTime;

                if (StateManager.FGTCurrentState == ToolsState.InCreative)
                    CurrentStats.TimeInFGC += Time.unscaledDeltaTime;

                if (SceneManager.GetActiveScene().name == "MainMenu")
                    CurrentStats.TimeInMenu += Time.unscaledDeltaTime;


                if (StateManager.FGTCurrentState == ToolsState.GameActive)
                    _roundLength += Time.unscaledDeltaTime;

                TimeElapsed += Time.unscaledDeltaTime;
                if (TimeElapsed >= SaveTime)
                {
                    TimeElapsed = 0;
                    Save();
                }
            }
        }

        void OnApplicationQuit() => Save();

        void OnTabChanged(TabMeta tab)
        {
            if (toggleHistory == null || RoundHistory == null)
                return;

            if (tab.Tab != Tab.Misc && RoundHistory.gameObject.activeSelf)
            {
                toggleHistory.ButtonText.text = LocalizedStr("gui_open_rhistory") + " ▼";
                RoundHistory.SetActive(false);
                HistoryActions.gameObject.SetActive(false);
            }
        }

        public override void DrawGUI()
        {
        }

        public void SetUIReferences(object[] data)
        {
            RoundHistoryTXT = (Text)data[0];
            HistoryMinus = (ButtonRef)data[1];
            DisplayInfo = (Text)data[2];
            HistoryPlus = (ButtonRef)data[3];
            toggleHistory = (ButtonRef)data[4];
            RoundHistory = (GameObject)data[5];
            HistoryActions = (GameObject)data[6];
        }

        public void RefreshUI()
        {

        }

        public void OnUIDestroy()
        {
            FGToolsUI.Instance.OnTabChanged -= OnTabChanged;
        }

        public void OnUICreated()
        {
            FGToolsUI.Instance.OnTabChanged += OnTabChanged;
        }
    }
}
