using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using UnityEngine;
using static FGTools.Internal.Extensions.FLZ_Extensions;
using UnityEngine.SceneManagement;
using static FGTools.Services.MenuThemeService;
using FG.Common.CMS;
using static FGTools.Services.LocalizationService;
using static FGTools.Services.StatisticsService;
using FGTools.Services.Logic;
using static FGTools.States.Logic.FGTStateManager;
using BepInEx.Logging;
using FGTools.UI;
namespace FGTools.Services
{
    internal class StatisticsService : FGTService
    {
        public StatJSON currentStats;
        public List<List<string>> HistoryPages = [];
        public enum RoundResult
        {
            NewRound,
            Qual,
            Elim,
            Win,
            Leave
        }

        public override void RegisterService()
        {
            Init(false);
            currentStats.GameLaunchedTimes++;
        }

        public void Init(bool cleanup)
        {
            FGTLog(LogLevel.Info, base.GetType(), $"init: cleanup = {cleanup}");
            if (cleanup && File.Exists(Launcher.StatsFile))
            {
                File.Delete(Launcher.StatsFile);
            }
            if (File.Exists(Launcher.StatsFile))
                currentStats = JsonSerializer.Deserialize<StatJSON>(File.ReadAllText(Launcher.StatsFile));
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
                currentStats = JsonSerializer.Deserialize<StatJSON>(File.ReadAllText(Launcher.StatsFile));
            }

            RefreshHistoryPages();
        }

        void RefreshHistoryPages()
        {
            if (HistoryPages != null)
            {
                HistoryPages.Clear();
                if (currentStats != null && currentStats.RoundHistory != null && currentStats.RoundHistory.Count > 0)
                {
                    for (int i = 0; i < currentStats.RoundHistory.Count; i += 50)
                    {
                        HistoryPages.Add(currentStats.RoundHistory.GetRange(i, Math.Min(50, currentStats.RoundHistory.Count - i)));
                    }

                    CurrHistoryPage = HistoryPages.Count - 1;
                }
                else
                {
                    if (NewGUI.Instance != null)
                        NewGUI.Instance.HistoryActions.gameObject.SetActive(false);
                    CurrHistoryPage = 0;
                }
            }
        }

        public void Save()
        {
            var stats = JsonSerializer.Serialize(currentStats);
            File.WriteAllText(Launcher.StatsFile, stats);
            FGTLog(LogLevel.Info, base.GetType(), "Save");
        }


        public string processedRoundID = "none";
        public readonly float SaveTime = 300f;
        public float timeElapsed = 0;
        float roundLength;
        Round currentRound;
        Round previousRound;
        int CurrHistoryPage;

        public void HistoryNavForward()
        {
            if (CurrHistoryPage < HistoryPages.Count - 1)
            {
                CurrHistoryPage++;
                LoadPage();
            }
        }

        public void HistoryNavBack()
        {
            if (CurrHistoryPage > 0)
            {
                CurrHistoryPage--;
                LoadPage();
            }
        }

        public void LoadPage()
        {
            if (HistoryPages.Count > 0)
            {
                NewGUI.Instance.DisplayInfo.text = $"{LocalizedStr("gui_page")} {CurrHistoryPage + 1} {LocalizedStr("gui_out_of")} {HistoryPages.Count}";
                int ab = CurrHistoryPage + 1 * HistoryPages[CurrHistoryPage].Count;
                List<string> sortedList = new([.. HistoryPages[CurrHistoryPage]]);
                sortedList.Reverse();
                NewGUI.Instance.RoundHistoryTXT.text = string.Join("\n", sortedList.ToArray().Select((line, index) => $"{ab -= 1}. | {line}"));
                NewGUI.Instance.HistoryPlus.Component.interactable = CurrHistoryPage + 1 != HistoryPages.Count;
                NewGUI.Instance.HistoryMinus.Component.interactable = CurrHistoryPage > 0;
                NewGUI.Instance.HistoryActions.gameObject.SetActive(NewGUI.Instance.CurrentTab.Tab == NewGUI.Tab.Misc && NewGUI.Instance.roundHistory.gameObject.activeSelf);
            }
            else
                NewGUI.Instance.HistoryActions.gameObject.SetActive(false);
        }

        public void AddFGCHistoryRound(string code)
        {
            if (currentStats.FGCSearchHistory == null)
                currentStats.FGCSearchHistory = new();

            currentStats.FGCSearchHistory.Add(code);

            NewGUI.Instance.RefreshFGCHistory(code);
            Save();
        }

        public void ProcessNewRound(RoundResult result)
        {
            if (currentRound != null)
                previousRound = currentRound;
       
            if (currentStats != null && previousRound != null && processedRoundID != currentRound.Id)
            {
                processedRoundID = previousRound.Id;
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

                string outstr = $"{CleanStr(previousRound.DisplayName.Text)} (<color={previousRound.Archetype.TagColour}>{previousRound.Archetype.Name.ToUpper()}</color>) | {LocalizedStr("gui_date")}: <color=grey>{DateTime.Now}</color> | {LocalizedStr("round_length")}: <color=grey>{TimeSpan.FromSeconds(roundLength):mm':'ss}</color> | {LocalizedStr("gui_result")}: {resultString}";
                if (currentStats.RoundHistory == null)
                    currentStats.RoundHistory = new();
                currentStats.RoundHistory.Add(outstr);
                RefreshHistoryPages();
                Save();
            }
        }

        public void SetNewRound(Round newRound)
        {
            currentRound = newRound;
            //processedRoundID = newRound.Id;
        }
        public void ResetTimer() => roundLength = 0;
        public void ValidateStats(string username)
        {
            var user = currentStats.StatsUser;
            if (user != null && user != "DEFAULT" && user != username)
                Init(true);
            else
                currentStats.StatsUser = username;
        }

        public override void UpdateService()
        {
            if (currentStats != null)
            {
                currentStats.TimeInGame += Time.unscaledDeltaTime;

                if (StateManager.FGTCurrentState == ToolsState.InCreative)
                    currentStats.TimeInFGC += Time.unscaledDeltaTime;

                if (SceneManager.GetActiveScene().name == "MainMenu")
                    currentStats.TimeInMenu += Time.unscaledDeltaTime;


                if (StateManager.FGTCurrentState == ToolsState.GameActive)
                    roundLength += Time.unscaledDeltaTime;

                timeElapsed += Time.unscaledDeltaTime;
                if (timeElapsed >= SaveTime)
                {
                    timeElapsed = 0;
                    Save();
                }
            }
        }

        void OnApplicationQuit() => Save();

        public override void DrawGUI()
        {
        }
    }
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
}
