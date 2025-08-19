using FG.Common.CMS;
using FG.Common.Definition;
using FGClient;
using FGTools.Services;
using FGTools.Services.Logic;
using FGTools.States.Logic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.SceneManagement;
using static FGTools.Config.ConfigManager;
using static FGTools.Internal.Extensions.FLZ_Extensions;
using static FGTools.UI.ReadyPopups;
using static FGTools.Services.LocalizationService;

namespace FGTools.States
{
    public class ShowState : FGTState
    {
        Show PlayingShow;
        int CurrentStage = 1;
        bool IsCurrentShowUseStages;
        int MaxStage;
        HashSet<string> PlayedRounds = new();
        HashSet<string> RemainingPool = new();

        public void OnNewShowSet(string ShowCMSID)
        {
            PlayingShow = CMSLoader.Instance._showsSO.Shows[ShowCMSID];

            if (PlayingShow != null)
            {
                foreach (var round in PlayingShow.DefaultEpisode.DefaultRoundPool.Stages)
                    RemainingPool.Add(round.Round.Id);
                RequestRoundFromShow();
            }
            else
                ErrorPopup($"{LocalizedStr("unable_to_find_show", [ShowCMSID])}");
            
        }

        void RequestRoundFromShow()
        {
            if (PlayingShow.DefaultEpisode == null)
                return;

            if (PlayingShow.DefaultEpisode.DefaultRoundPool == null)
                return;

            foreach (var pool in PlayingShow.DefaultEpisode.DefaultRoundPool.Stages)
            {
                if (pool.CanOnlyBeOnTheseStages.Count > 0 || pool.CannotBeOnTheseStages.Count > 0)
                {
                    if (pool.CanOnlyBeOnTheseStages.Count > 0 && MaxStage < pool.CanOnlyBeOnTheseStages.Max())
                        MaxStage = pool.CanOnlyBeOnTheseStages.Max();

                    if (pool.CannotBeOnTheseStages.Count > 0 && MaxStage < pool.CannotBeOnTheseStages.Max())
                        MaxStage = pool.CannotBeOnTheseStages.Max();

                    IsCurrentShowUseStages = true;
                    //break;
                }
            }

            List<Round> TotalRounds = [];
            //FGTGUI.NewGUI.instance.loadedShow = PlayingShow.ShowName;
            if (IsCurrentShowUseStages)
            {
                var possibleRounds = PlayingShow.DefaultEpisode.DefaultRoundPool.GetAllPossibleRoundsForStage(CurrentStage);
                foreach (var possibleRound in possibleRounds)
                {
                    if (RemainingPool.Contains(possibleRound.Round.Id))
                        TotalRounds.Add(possibleRound.Round);
                }

                if (possibleRounds.Count == 0)
                {
                    FGTLog(BepInEx.Logging.LogLevel.Warning, base.GetType(), $"Show {PlayingShow.ShowName} doesn't have any rounds on stage {CurrentStage}");
                    TriggerNoRoundsModal();
                    return;
                }
            }
            else
            {
                foreach (var round in PlayingShow.DefaultEpisode.DefaultRoundPool.Stages)
                    TotalRounds.Add(round.Round);
            }

            FGTLog(BepInEx.Logging.LogLevel.Info, base.GetType(), $"Got {TotalRounds.Count} rounds");

            var target = FGTRoundLoader.GetFiltredRounds(TotalRounds);
            if (target.Count > 0)
            {
                var newRound = target.ElementAt(UnityEngine.Random.Range(0, target.Count));

                if (IsRoundValid(newRound))
                    FGTRoundLoader.LoadCMSRound(newRound, LoadSceneMode.Single);
                else
                    FGTLog(BepInEx.Logging.LogLevel.Error, base.GetType(), $"New round is not valid {newRound}");
            }
            else
                TriggerNoRoundsModal();
        }

        void TriggerNoRoundsModal() => DoModal(LocalizedStr("gui_show_no_rounds_title"), LocalizedStr("gui_show_no_rounds_desc"), FGClient.UI.UIModalMessage.ModalType.MT_OK, FGClient.UI.UIModalMessage.OKButtonType.Default, new Action<bool>(Quit), hideGUI: ModalHideGUIType.KeepHiddenForThisModal);

        void Quit(bool wasok = true)
        {
            OnStateExit();
            if (SceneManager.GetActiveScene().name != "MainMenu")
                LeaveMatchPopupManager.Instance.OnClose(true);
        }

        public bool IsRoundValid(string round)
        {
            if (!RemainingPool.Contains(round) && !PlayedRounds.Contains(round))
            {
                FGTLog(BepInEx.Logging.LogLevel.Warning, base.GetType(), $"Got round tnat not in a show ({round}), qutting show state");
                OnStateExit();
                return false;
            }

            if (PlayedRounds.Contains(round))
                return false;

            PlayedRounds.Add(round);
            RemainingPool.Remove(round);
            return true;
        }

        public void OnShowProgress()
        {
            CurrentStage++;
            if (CurrentStage > MaxStage && MaxStage > 0)
                TryToEndGameplay();
            else
            {
                if (RemainingPool.Count > 0)
                    RequestRoundFromShow();
                else
                    TryToEndGameplay();
            }
        }

        void TryToEndGameplay()
        {
            //TODO: winning
            Quit();
        }

        public override void DisplayGUI()
        {

        }

        public override void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {

        }

        public override void OnStateExit()
        {
            StateManager.ShowState = null;
            GC.Collect();
        }

        public override void OnStateSet()
        {
         
        }

        public override void UpdateState()
        {

        }
    }
}
