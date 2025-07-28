extern alias wle;
using System;
using BepInEx.Logging;
using Events;
using FG.Common.Fraggle;
using FGClient;
using FGTools.Content;
using FGTools.Services.Logic;
using UnityEngine;
using static FGTools.Config.ConfigManager;
using static FGTools.Internal.Extensions.FLZ_Extensions;
using static FGTools.Services.LocalizationService;
namespace FGTools.Services
{
    internal class FGC_AutosaveService : FGTService
    {
        const float ResultDisplayTime = 5f;

        DateTime NextAutosaveTime = DateTime.MinValue;
        DateTime ResultDisplay = DateTime.MinValue;
        readonly EventSystem.Handle OnEnter;
        readonly EventSystem.Handle OnExit;
        readonly EventSystem.Handle OnSaved;
        LevelEditorSavedEvent.Result LatestAutosaveResult;
        bool PendingAutosaveAction = false;
        bool ShouldDisplayResult = false;
        bool IsInEditor = false;
        static bool IsInExploreState => wle.FG.Common.LevelEditorManager.Instance.IsInExplore();

        public override void UpdateService()
        {
            if (IsInEditor && allowFGCAutosaves.Value)
            {
                if (NextAutosaveTime != DateTime.MinValue && DateTime.Now >= NextAutosaveTime && FGTTargetSettings.FGCAutosaves)
                {
                    NextAutosaveTime = DateTime.MinValue;
                    PendingAutosaveAction = true;
                    wle.FG.Common.LevelEditorManagerUI.OnSaveRetry(true);
                }
            }
        }

        void EnterEvent(LevelEditorEnterEvent e)
        {
            if (FGTTargetSettings.FGCAutosaves)
                NextAutosaveTime = DateTime.Now.AddSeconds(targetFGCSaveTime.Value);

            PendingAutosaveAction = false;
            ShouldDisplayResult = false;
            IsInEditor = true;
        }

        void ExitEvent(LevelEditorExitEvent e)
        {
            NextAutosaveTime = DateTime.MinValue;
            PendingAutosaveAction = false;
            ShouldDisplayResult = false;
            IsInEditor = false;
        }

        void LevelSaved(LevelEditorLevelSavedEvent e)
        {
            FGTLog(LogLevel.Info, base.GetType(), $"Level save ended with result \"{e.Results}\". Was it autosave: {PendingAutosaveAction}");

            if (e.Results == LevelEditorSavedEvent.Result.No_Error || PendingAutosaveAction)
                NextAutosaveTime = DateTime.Now.AddSeconds(targetFGCSaveTime.Value);
            
            if (PendingAutosaveAction)
            {
                FGTServiceManager.GetService<FGC_LocalSavesService>().TryAutosaveLevel();
                PendingAutosaveAction = false;
                LatestAutosaveResult = e.Results;
                ShouldDisplayResult = true;
            }
        }

        void OnExploreEnter(LevelEditorEnteredExploreModeEvent e)
        {

        }

        void OnExploreExit(LevelEditorEnteredBuildModeFromExploreModeEvent e)
        {

        }

        public override void RegisterService()
        {
            Broadcaster.Instance.Register<LevelEditorEnterEvent>(new Action<LevelEditorEnterEvent>(EnterEvent));
            Broadcaster.Instance.Register<LevelEditorExitEvent>(new Action<LevelEditorExitEvent>(ExitEvent));
            Broadcaster.Instance.Register<LevelEditorLevelSavedEvent>(new Action<LevelEditorLevelSavedEvent>(LevelSaved));
            Broadcaster.Instance.Register<LevelEditorEnteredExploreModeEvent>(new Action<LevelEditorEnteredExploreModeEvent>(OnExploreEnter));
            Broadcaster.Instance.Register<LevelEditorEnteredBuildModeFromExploreModeEvent>(new Action<LevelEditorEnteredBuildModeFromExploreModeEvent>(OnExploreExit));
        }

        public override void DrawGUI()
        {
            if (IsInEditor && allowFGCAutosaves.Value && showAutosaveTimer.Value)
            {
                TimeSpan autosave_left = NextAutosaveTime - DateTime.Now;
                string label;

                if (FGTTargetSettings.FGCAutosaves)
                {
                    if (autosave_left > TimeSpan.Zero)
                        label = $"<b>{LocalizedStr("gui_fgc_autosave_in")}: {autosave_left:mm':'ss}</b>";
                    else
                        label = $"<b>{LocalizedStr("gui_fgc_autosave_going")}</b>";
                }
                else
                    label = $"<b>{LocalizedStr("gui_disabled_feature")}</b>";

                if (ShouldDisplayResult)
                {
                    if (ResultDisplay == DateTime.MinValue)
                        ResultDisplay = DateTime.Now.AddSeconds(5);

                    TimeSpan resultDisplay_left = ResultDisplay - DateTime.Now;

                    if (resultDisplay_left > TimeSpan.Zero)
                        label = $"<b>{LocalizedStr("gui_fgc_autosave_result", [LatestAutosaveResult])}</b>";
                    else
                    {
                        ResultDisplay = DateTime.MinValue;
                        ShouldDisplayResult = false;
                    }
                }

                GUI.Box(new Rect(-3, Screen.height - 20f - 3, GUI.skin.label.CalcSize(new(label)).x + 10, 40), "");

                Rect labelRect = new Rect(3, Screen.height - 20f - 3, 9999f, 30f);
                GUI.Label(labelRect, label);
            }
        }
    }
}
