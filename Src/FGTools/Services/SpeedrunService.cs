using BepInEx.Logging;
using FG.Common;
using FG.Common.Audio;
using FGClient;
using FGClient.Rendering.XRay;
using FGClient.UI;
using FGTools.Config;
using FGTools.HarmonyPatches;
using FGTools.Internal;
using FGTools.Services.Logic;
using FGTools.UI;
using FMOD.Studio;
using Levels.Progression;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using static FGTools.Config.ConfigManager;
using static FGTools.Internal.Extensions.FLZ_Extensions;
using static FGTools.Services.LocalizationService;
using static FGTools.States.Logic.FGTStateManager;
using static FGTools.UI.ReadyPopups;

namespace FGTools.Services
{
    internal class SpeedrunService : FGTService
    {
        public class SpeedrunSaveJson
        {
            public Dictionary<string, float> saveData { get; set; }
        }

        internal class Speedrun(int attempt)
        {
            public struct Progression(SpeedrunProgressionType type, float value)
            {
                public SpeedrunProgressionType Type = type;
                public float Value = value;
            }

            public int Attempt = attempt;
            public float RunningTime;
            public Dictionary<int, Progression> CheckpointProgression = [];

            public void AddProgression(SpeedrunProgressionType type, int num, float reachTime)
            {
                CheckpointProgression[num] = new(type, reachTime);
            }

            public override string ToString()
            {
                return $"{Attempt} - {RunningTime} [{CheckpointProgression.Count}]";
            }
        }

        internal bool IsSepeedrunsDisabled => SpeedrunState == RunState.TimeAttack || SpeedrunState == RunState.TempDisabled;

        string _timerText;
        int _checkpointNum = 0;
        int _lapNum = 0;
        StringBuilder _stat = new();
        float _previousSaveTime;
        GameObject _timerObject;
        GameObject _checkpointPopup;
        SkipRoundButton _restartButton;
        GameObject _splitTimeText;
        GameObject _lapTimeText;
        float _lastSceneTime = 0;
        bool _allowTimerBeActive;
        TimeAttackLapDisplay _display;
        string _infoStat;
        string LatestRunScene;
        SpeedrunSaveJson latestSave;
        EventInstance SnapshotEvent;

        Speedrun CurrentRun;
        List<Speedrun> RunsHistory = [];

        int AmountOfSpawns = 0;

        public override void RegisterService()
        {
            LoadData();
            Commands.OnLapComplete += new System.Action(() => { SaveRunTimer(SpeedrunProgressionType.Lap); });
            Commands.OnCheckpointReached += new System.Action<MPGNetObject, CheckpointZone>((MPGNetObject obj, CheckpointZone zone) => 
            {
                if (SpeedrunState != RunState.Running)
                    return;

                SaveRunTimer(SpeedrunProgressionType.Checkpt, zone);
            });
            Commands.OnQualified += new System.Action(() => 
            { 
                SaveRunTimer(SpeedrunProgressionType.Qual); 
            });
            Commands.OnEliminated += new System.Action(() => 
            { 
                SaveRunTimer(SpeedrunProgressionType.Elim); 
            });
            Commands.OnWon += new System.Action(() => 
            { 
                SaveRunTimer(SpeedrunProgressionType.Win); 
            });
        }

        float FindFastestProgression(int checkpoint)
        {
            var time = RunsHistory.Where(run => run.CheckpointProgression.TryGetValue(checkpoint, out var prog) && prog.Type == SpeedrunProgressionType.Checkpt).Select(run => run.CheckpointProgression[checkpoint].Value).DefaultIfEmpty().Min();
            return time == 0 ? CurrentRun.RunningTime : time;
        }

        void EndCurrentRun()
        {
            if (CurrentRun == null)
                return;

            RunsHistory.Add(CurrentRun);
            CurrentRun = null;
        }

        void TryToConvertOldData()
        {
            if (!File.Exists(Plugin.RunsData))
                return;

            var all = File.ReadAllLines(Plugin.RunsData);
            foreach (string line in all)
            {
                var split = line.Split(':');
                latestSave.saveData[split[0]] = float.Parse(split[1]);
            }
            FGTLog(LogLevel.Info, base.GetType(), $"CONVERT: Total lines parsed: {all.Length}. New lines: {latestSave.saveData.Count}");
            File.Delete(Plugin.RunsData);
            WriteSave();
        }

        void WriteSave() => File.WriteAllText(Plugin.RunsDataNew, JsonSerializer.Serialize<SpeedrunSaveJson>(latestSave));

        public void LoadData()
        {
            try
            {
                if (File.Exists(Plugin.RunsDataNew))
                {
                    latestSave = JsonSerializer.Deserialize<SpeedrunSaveJson>(File.ReadAllText(Plugin.RunsDataNew));
                }
                else
                {
                    latestSave = new()
                    {
                        saveData = []
                    };
                    TryToConvertOldData();
                }
            }
            catch
            {
                File.Delete(Plugin.RunsDataNew);
                LoadData();
            }
        }

        public enum SpeedrunProgressionType
        {
            Checkpt,
            Qual,
            Elim,
            Win,
            Lap,
            None
        }

        public enum RunState
        {
            Inactive,
            Respawned,
            Running,
            Finish,
            TimeAttack,
            TempDisabled
        }

        public RunState SpeedrunState;


        public string ReturnTimerText()
        {
            return _timerText;
        }

        public float ReturnCurrentTime()
        {
            return CurrentRun.RunningTime;
        }

        public string ReturnLatestScene()
        {
            return LatestRunScene;
        }

        public void SetLatestSceneTime(float data)
        {
            _lastSceneTime = data;
        }

        public string ReturnLatestSceneTime()
        {
            return ReturnTimeAsString(_lastSceneTime, false, true);
        }

        public void TriggerSpeedrunContinueModal(bool winScreen = false, VictoryScreenViewModel player = null)
        {
            if (StateManager.IsFGC)
                SpeedrunContinePopup(GetRunTime(CGM._round.Id), winScreen, player);
            else
                SpeedrunContinePopup(GetRunTime(LatestRunScene), winScreen, player);
        }

        internal void TriggerSpeedrunRestart()
        {
            if (QualLevel.Value == QualType.LoadRandomRoundAfter)
                SpeedrunRestart();
        }

        public string CreateSplitTimeText(float splitTime, bool isPositive)
        {
            string locText = Resources.FindObjectsOfTypeAll<LocalisedStrings>().FirstOrDefault().GetString(isPositive ? "timeattack_split_result_slower" : "timeattack_split_result_faster");
            string splitText = (isPositive ? "+ " : "") + ReturnTimeAsString(splitTime, false, true, true);
            return string.Format(locText, splitText);
        }

        public void UpdateTimer(bool debug)
        {
            if (!debug)
                CurrentRun.RunningTime += GameStateView.Instance.SimulationDeltaTime;
            else
                CurrentRun.RunningTime += Time.deltaTime;

            if (SpeedrunState != RunState.Respawned)
                try { _lapTimeText.GetComponent<TextMeshProUGUI>().SetText($"{ReturnTimeAsString(CurrentRun.RunningTime, true)}"); } catch { }
            else
                try { _lapTimeText.GetComponent<TextMeshProUGUI>().SetText($"{ReturnTimeAsString(0, true)}"); } catch { }

        }

        internal void PrepareForGameplay()
        {
            RunsHistory.Clear();

            LoadUI();
            HandleState(RunState.Respawned);

            if (!FMODTool.CreateFMODEvent("SFX_TimeAttack_Snapshot_TimeStop", out SnapshotEvent))
                FGTLog(LogLevel.Warning, GetType(), "Unable to create snapshot event");
            else
                SnapshotEvent.start();
        }

        public void LoadUI()
        {
            if (CGM == null)
                return;

            var UIManager = CGM._inGameUiManager.gameObject;

            _timerObject = GetChild(UIManager, "GameplayTimeAttackViewModel");

            if (_timerObject == null)
            {
                ReadyPopups.ErrorPopup("skibidi ohio sigma");
                return;
            }

            _timerObject.SetActive(true);
            _checkpointPopup = GetChild(UIManager, "SplitTime");
            _splitTimeText = GetChild(UIManager, "SplitTimeText");
            _splitTimeText.gameObject.SetActive(false);
            _restartButton = GetChild(CGM._inGameUiManager._inGameUiStates[2].gameObject, "ResetTimeAttackLap").GetComponent<SkipRoundButton>();
            _lapTimeText = GetChild(UIManager, "LapTimeText");
            _restartButton?.gameObject.SetActive(false);
            _restartButton?.SetHoldTimeRequired(ConfigManager.SPRespawnCD.Value);
            var lap = GetChild(UIManager, "PB_UI_TimeAttack_LapTimer");
            if (lap != null)
            {
                _display = lap.GetComponent<TimeAttackLapDisplay>();
                _display.Init(CGM);
            }
            if (CGM.GameRules.ScoreDisplayMode != ScoreDisplayModes.None)
            {
                if (!FGTServiceManager.GetService<RoundOptionsService>().ReturnLatestOptions().TimeLimit)
                {
                    _timerObject.transform.localPosition = new Vector3(750, 0, 0);
                    _checkpointPopup.transform.localPosition = new Vector3(-450, 0, 0);
                }
                else
                {
                    //if (useIngameObjective.Value)
                    //{
                    _timerObject.transform.localPosition = CGM.GameRules.TeamCount switch
                    {
                        1 => new Vector3(-250f, 0, 0),
                        2 => new Vector3(-400f, 0, 0),
                        3 => new Vector3(-450f, 0, 0),
                        4 => new Vector3(-490f, 0, 0),
                        _ => new Vector3(-400f, 0, 0),
                    };
                    //}
                    //else
                    //    _timerObject.transform.localPosition = new Vector3(-750, 0, 0);
                }
            }

            _display._timeAttackIsTimerPaused = false;
            _display._timeAttackLapTimeAnimation.Play("UI_HUD_TimeAttack_LapTime_Base");
            _display.TryTimeAttackPulseTimer(new(102, null));
        }

        void OnApplicationFocus(bool hasFocus)
        {
            if (SpeedrunState == RunState.Running)
                TriggerTimer(hasFocus);
        }

        public void TriggerTimer(bool on)
        {
            _allowTimerBeActive = on;
        }

        public override void UpdateService()
        {
            if (StateManager.IsInGameplay && SpeedrunMode.Value && _allowTimerBeActive)
            {

                if (SpeedrunState == RunState.Running)
                    UpdateTimer(false);

                if (SpeedrunState == RunState.Respawned && FGBehaviour != null && FGBehaviour.FallGuy)
                {
                    if (!SPInstaStart.Value)
                    {
                        var wheel = FGBehaviour.FGCC.DiveMotorTask;
                        var chair = FGBehaviour.FGCC.MoveMotorTask;

                        if (FGBehaviour.FGCC.CanMove && chair != null && wheel != null && chair.isRequested || wheel.isRequested)
                            HandleState(RunState.Running);
                    }
                    else
                        HandleState(RunState.Running);
                }
            }
        }

        public void HandleState(RunState newState)
        {
            if (IsSepeedrunsDisabled && (newState == RunState.TempDisabled || newState == RunState.TimeAttack || newState == RunState.Inactive))
                return;

            SpeedrunState = newState;
            switch (newState)
            {
                case RunState.Respawned:
                    EndCurrentRun();
                    AmountOfSpawns++;
                    TriggerTimer(true);
                    if (SnapshotEvent.hasHandle())
                        SnapshotEvent.start();
                    _lapTimeText.GetComponent<TextMeshProUGUI>().SetText(ReturnTimeAsString(0, true, true, false));
                    ResetStats();
                    if (_display != null)
                    {
                        _display._currentLocalTimeAttackLapState = TimeAttackLapState.NotStarted;
                        _restartButton?.gameObject.SetActive(false);
                        _display._timeAttackIsTimerPaused = false;
                        _display._timeAttackLapTimeAnimation.Play("UI_HUD_TimeAttack_LapTime_Base");
                        _display.TryTimeAttackPulseTimer(new(102, null));
                    }
                    _infoStat = $"{LocalizedStr("gui_attempt")}: <b>{RunsHistory.Count + 1}</b>";

                    var name = !StateManager.IsFGC ? SceneManager.GetActiveScene().name : CGM._round.Id;

                    if (IsThereBestTime(name))
                        _infoStat += $" | {LocalizedStr("gui_best_time")}: <b>{ReturnTimeAsString(GetRunTime(name), false, true)}</b>";
                    break;
                case RunState.Finish:
                    TriggerTimer(false);
                    if (SnapshotEvent.hasHandle())
                        SnapshotEvent.start();
                    _display._currentLocalTimeAttackLapState = TimeAttackLapState.Finished;
                    _restartButton?.gameObject.SetActive(false);
                    CGM.SetClockPaused(true);
                    CGM._physicsSimulator.SetRunningPhysicsAutomatically(false);
                    XRayUtils.RemoveXRayControllerForCharacter(FGBehaviour.FGCC);
                    FGBehaviour.FGCC.RigidBody.isKinematic = true;
                    FGBehaviour.FGCC.CustomisationHandler.HandleCostumeVisibility(false);
                    FGBehaviour.FGCC.CustomisationHandler.HandleFallGuyVisibility(false);
                    break;
                case RunState.Running:
                    CurrentRun = new(RunsHistory.Count + 1);
                    LatestRunScene = SceneManager.GetActiveScene().name;
                    TriggerTimer(true);

                    if (SnapshotEvent.hasHandle())
                        SnapshotEvent.stop(STOP_MODE.IMMEDIATE);

                    if (SPRespawnCD.Value >= 0.3f)
                        AudioManager.PlayOneShot(AudioManager.Instance._eventMasterData.TimeAttackTimeStart);

                    if (StateManager.FGCurrentState != PlayerState.FreeCam)
                        AudioMixing.Instance.ResetTimeAttackParams();

                    if (_display != null)
                    {
                        _display._currentLocalTimeAttackLapState = TimeAttackLapState.InProgress;
                        _restartButton?.gameObject.SetActive(true);
                        _restartButton?.SetHoldTimeRequired(ConfigManager.SPRespawnCD.Value);
                        _display._timeAttackIsTimerPaused = false;
                        _display._timeAttackLapTimeAnimation.Play("UI_HUD_TimeAttack_LapTime_Base");
                        _display.TryTimeAttackPulseTimer(new(102, null));
                        _display._timeAttackLapTimeAnimation.Play();
                    }
                    break;
                case RunState.Inactive:
                    TriggerTimer(false);

                    if (_timerObject != null)
                        _timerObject?.SetActive(false);

                    _restartButton?.gameObject.SetActive(false);
                    _restartButton = null;
                    break;
                case RunState.TimeAttack:
                    TriggerTimer(false);
                    break;
                case RunState.TempDisabled:
                    _timerObject.gameObject.SetActive(false);
                    _restartButton?.gameObject.SetActive(false);
                    TriggerTimer(false);
                    break;
            }
        }

        public override void DrawGUI()
        {
            if (SpeedrunMode.Value && SpeedrunUI.Value && (SpeedrunState != RunState.TimeAttack || SpeedrunState != RunState.TempDisabled))
            {
                if (SpeedrunState == RunState.Running || SpeedrunState == RunState.Finish)
                {
                    var builder = new StringBuilder();
                    builder.AppendLine($"<b>{LocalizedStr("gui_run_info").ToUpper()}</b>");
                    builder.AppendLine(_stat.ToString());
                    builder.AppendLine(_infoStat);

                    string text = builder.ToString();
                    var labSize = GUI.skin.label.CalcSize(new GUIContent(text));

                    var labelX = Screen.width - 300 + 5 * 2;
                    var labelY = Screen.height - labSize.y + 5 * 2;

                    GUI.Box(new Rect(labelX, labelY, 300 + 5 * 2, labSize.y + 5 * 2), "");
                    GUI.Box(new Rect(labelX, labelY, 300 + 5 * 2, labSize.y + 5 * 2), "");

                    GUI.Label(new Rect(labelX + 5, labelY + 5, 300, labSize.y), text);
                }
            }

        }

        string CalculateTimeDifference(float param1, float param2)
        {
            var dif = param1 - param2;
            var sign = dif == 0 ? string.Empty : (dif > 0 ? "+" : "-");

            return $"{sign}{(int)(Mathf.Abs(dif) / 60):00}:{(int)(Mathf.Abs(dif) % 60):00}.{(int)(Mathf.Abs(dif) * 1000) % 1000:000}";
        }



        public void SaveRunTimer(SpeedrunProgressionType type, CheckpointZone zone = null)
        {
            if (!SpeedrunMode.Value || IsSepeedrunsDisabled)
                return;

            switch (type)
            {
                case SpeedrunProgressionType.Checkpt:
                    _checkpointNum += 1;
                    _stat.AppendLine($"{LocalizedStr("gui_checkpoint")} №{_checkpointNum} ({_timerText}) [{CalculateTimeDifference(CurrentRun.RunningTime, FindFastestProgression((int)zone.uniqueId))}]");
                    _checkpointPopup?.SetActive(true);
                    _splitTimeText?.SetActive(true);
                    _splitTimeText?.GetComponent<TextMeshProUGUI>().SetText(_timerText);
                    CurrentRun.AddProgression(type, (int)zone.uniqueId, CurrentRun.RunningTime);
                    break;
                case SpeedrunProgressionType.Lap:
                    _lapNum += 1;
                    _stat.AppendLine($"{LocalizedStr("gui_pizzatowerreference")} №{_lapNum}");
                    _checkpointPopup?.SetActive(true);
                    _splitTimeText?.SetActive(true);
                    _splitTimeText?.GetComponent<TextMeshProUGUI>().SetText(_timerText);
                    break;
                case SpeedrunProgressionType.Qual:
                    _stat.AppendLine($"{LocalizedStr("gui_run_qual")} {_timerText}");
                    break;
                case SpeedrunProgressionType.Elim:
                    _stat.AppendLine($"{LocalizedStr("gui_run_elim")} {_timerText}");
                    break;
                case SpeedrunProgressionType.Win:
                    _stat.AppendLine($"{LocalizedStr("gui_run_win")} {_timerText}");
                    break;
                case SpeedrunProgressionType.None:
                    break;
            }

            _previousSaveTime = CurrentRun.RunningTime;
        }

        public string ReturnTimeAsString(float time, bool format = false, bool doNotSetTimerText = false, bool useScaleFactor = false, float scale = 80f)
        {
            string sign = (time < 0) ? " - " : "";

            string timeString = string.Format("{0}{1:00}:{2:00}.{3:000}", sign, Mathf.Abs((int)time) / 60, Mathf.Abs((int)time) % 60, Mathf.Abs((int)(time * 1000)) % 1000);

            if (!doNotSetTimerText)
                _timerText = timeString;

            if (!format && !useScaleFactor)
            {
                return timeString;
            }
            else if (useScaleFactor)
            {
                return string.Format("{0}{1:00}:{2:00}<size={3}%>.{4:000}</size>", sign, Mathf.Abs((int)time) / 60, Mathf.Abs((int)time) % 60, scale, Mathf.Abs((int)(time * 1000)) % 1000);
            }
            else if (format)
            {
                return string.Format("<mspace=33>{0:00}<mspace=15>:<mspace=33>{1:00}<mspace=15>.<mspace=23.5><size=70%>{2:000}", Mathf.Abs((int)time) / 60, Mathf.Abs((int)time) % 60, Mathf.Abs((int)(time * 1000)) % 1000);
            }

            return timeString;
        }

        public void ResetStats()
        {
            _previousSaveTime = 0;
            _checkpointNum = 0;
            _lapNum = 0;
            _stat.Clear();
        }

        public void NewRun()
        {
            HandleState(RunState.Respawned);
            
            var cm = Resources.FindObjectsOfTypeAll<CheckpointManager>().FirstOrDefault();
            cm?._netIDToCheckpointMap.Clear();

            var ez = Resources.FindObjectsOfTypeAll<COMMON_ObjectiveReachEndZone>().FirstOrDefault();
            ez?._charactersAchievingObjective.Clear();

            if (SPResetPoints.Value && CGM.GameRules.IsScoringGame)
                ServerGameStateActions.Instance.AwardPoints(FGBehaviour.FGMPG, CGM._soloScoreManager.GetSoloScore(FGBehaviour.FGMPG.NetID) * -1);
        }

        public void DoRunSave()
        {
            if (!StateManager.IsFGC)
                SaveRun(LatestRunScene, CurrentRun.RunningTime);
            else
                SaveRun(CGM._round.Id, CurrentRun.RunningTime);
        }

        bool IsThereBestTime(string forScene)
        {
            return latestSave.saveData.ContainsKey(forScene);
        }

        void SaveRun(string scene, float totalTime)
        {
            latestSave.saveData[scene] = totalTime;
            WriteSave();
        }

        public float GetRunTime(string forScene)
        {
            if (latestSave.saveData.ContainsKey(forScene))
                return latestSave.saveData[forScene];
            else
                return -1f;
        }
    }
}
