using BepInEx.Logging;
using FG.Common;
using FG.Common.Audio;
using FGClient;
using FGClient.UI;
using FGTools.Config;
using FGTools.HarmonyPatches;
using FGTools.Internal;
using FGTools.Services.Logic;
using FGTools.UI;
using Levels.Progression;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
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
        public override void RegisterService()
        {
            LoadData();
            Commands.OnLapComplete += new System.Action(() => { SaveRunTimer(SpeedrunSaveType.Lap); });
            Commands.OnCheckpointReached += new System.Action<MPGNetObject>(netObj => { SaveRunTimer(SpeedrunSaveType.Checkpt); });
            Commands.OnQualified += new System.Action(() => { SaveRunTimer(SpeedrunSaveType.Qual); });
            Commands.OnEliminated += new System.Action(() => { SaveRunTimer(SpeedrunSaveType.Elim); });
            Commands.OnWon += new System.Action(() => { SaveRunTimer(SpeedrunSaveType.Win); });
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
                        saveData = new Dictionary<string, float>()
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

        //float speedrunTime = 0f;
        string _timerText;
        //string _minText;
        //string _secText;
        //string _milText;
        float _currentTime;
        //private int _minutes, _seconds, _milliseconds;
        int _checkpointNum = 0;
        int _lapNum = 0;
        string _stat = string.Empty;
        float _previousSaveTime;
        GameObject _timerObject;
        GameObject _checkpointPopup;
        SkipRoundButton _restartButton;
        GameObject _splitTimeText;
        GameObject _lapTimeText;
        float _lastSceneTime = 0;
        float baseBoxPos = 10f;
        Vector3 _spawnPos;
        Quaternion _spawnRot;
        bool _allowTimerBeActive;
        TimeAttackLapDisplay _display;
        //int _attNum = -1;
        string _infoStat;
        string LatestRunScene;
        SpeedrunSaveJson latestSave;
        internal bool IsSepeedrunsDisabled => SpeedrunState == RunState.TimeAttack || SpeedrunState == RunState.TempDisabled;

        int Attempt = 1;
        int AmountOfSpawns = 0;
        public enum SpeedrunSaveType
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

        public void SetSpawnPos(Vector3 pos, Quaternion rot)
        {
            _spawnPos = pos;
            _spawnRot = rot;
        }

        public string ReturnTimerText()
        {
            return _timerText;
        }

        public float ReturnCurrentTime()
        {
            return _currentTime;
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
                _currentTime += GameStateView.Instance.SimulationDeltaTime;
            else
                _currentTime += Time.deltaTime;

            if (SpeedrunState != RunState.Respawned)
                try { _lapTimeText.GetComponent<TextMeshProUGUI>().SetText($"{ReturnTimeAsString(_currentTime, true)}"); } catch { }
            else
                try { _lapTimeText.GetComponent<TextMeshProUGUI>().SetText($"{ReturnTimeAsString(0, true)}"); } catch { }

        }
        public void LoadUI()
        {
            if (CGM != null)
            {
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
                if (CGM != null && CGM.GameRules.ScoreDisplayMode != ScoreDisplayModes.None)
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
            }
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
            if (StateManager.FGTCurrentState == FGTState.GameActive || StateManager.FGTCurrentState == FGTState.FGCGameActive && SpeedrunMode.Value && _allowTimerBeActive)
            {

                if (SpeedrunState == RunState.Running)
                {
                    UpdateTimer(false);
                }

                if (SpeedrunState == RunState.Respawned && FGBehaviour != null && FGBehaviour.FallGuy)
                {
                    if (!SPInstaStart.Value)
                    {
                        var chair = FGBehaviour.FallGuy.GetComponent<FallGuysCharacterController>().MoveMotorTask;
                        var wheel = FGBehaviour.FallGuy.GetComponent<FallGuysCharacterController>().DiveMotorTask;
                        if (chair != null && wheel != null && chair.isRequested || wheel.isRequested)
                            HandleState(RunState.Running);
                    }
                    else
                        HandleState(RunState.Running);
                }
            }
        }

        public void HandleState(RunState newState)
        {
            RunState prevState = SpeedrunState;

            if (prevState != RunState.TimeAttack || prevState != RunState.TempDisabled)
                SpeedrunState = newState;

            switch (newState)
            {
                case RunState.Respawned:
                    AmountOfSpawns++;
                    TriggerTimer(true);
                    AudioMixing.Instance.StartTimeAttackSnapshot();
                    _lapTimeText.GetComponent<TextMeshProUGUI>().SetText(ReturnTimeAsString(0, true, true, false));
                    //if (prevState == RunState.Running || prevState == RunState.Finish)
                    ResetStats();
                    if (_display != null)
                    {
                        _display._currentLocalTimeAttackLapState = TimeAttackLapState.NotStarted;
                        //_display.ShouldShowTimeAttackResetInput = false;
                        _restartButton?.gameObject.SetActive(false);
                        _display._timeAttackIsTimerPaused = false;
                        _display._timeAttackLapTimeAnimation.Play("UI_HUD_TimeAttack_LapTime_Base");
                        _display.TryTimeAttackPulseTimer(new(102, null));
                    }
                    _infoStat = $"{LocalizedStr("gui_attempt")}: <b>{Attempt}</b>";
                    if (!StateManager.IsFGC)
                    {
                        if (IsThereBestTime(SceneManager.GetActiveScene().name))
                            _infoStat += $" | {LocalizedStr("gui_best_time")}: <b>{ReturnTimeAsString(GetRunTime(SceneManager.GetActiveScene().name), false, true)}</b>";
                    }
                    else
                    {
                        if (IsThereBestTime(CGM._round.Id))
                            _infoStat += $" | {LocalizedStr("gui_best_time")}: <b>{ReturnTimeAsString(GetRunTime(CGM._round.Id), false, true)}</b>";
                    }
                    break;
                case RunState.Finish:
                    TriggerTimer(false);
                    AudioMixing.Instance.StartTimeAttackSnapshot();
                    _display._currentLocalTimeAttackLapState = TimeAttackLapState.Finished;
                    _restartButton?.gameObject.SetActive(false);
                    CGM.SetClockPaused(true);
                    CGM._physicsSimulator.SetRunningPhysicsAutomatically(false);
                    break;
                case RunState.Running:
                    Attempt++;
                    LatestRunScene = SceneManager.GetActiveScene().name;
                    TriggerTimer(true);
                    if (SPRespawnCD.Value >= 0.3f)
                        AudioManager.PlayOneShot(AudioManager.Instance._eventMasterData.TimeAttackTimeStart);
                    if (StateManager.FGCurrentState != PlayerState.FreeCam)
                        AudioMixing.Instance.ResetTimeAttackParams();
                    if (_display != null)
                    {
                        _display._currentLocalTimeAttackLapState = TimeAttackLapState.InProgress;
                        //_display.ShouldShowTimeAttackResetInput = true;
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
                    AttackOfTheTime.Display._currentLocalTimeAttackLapState = TimeAttackLapState.NotStarted;
                    //AttackOfTheTime.Display.ShouldShowTimeAttackResetInput = false;
                    if (_restartButton != null)
                        _restartButton.gameObject.SetActive(false);
                    _restartButton = null;
                    Attempt = 1;
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
                float labelX = Screen.width - 240;
                float labelY = Screen.height - 40 - baseBoxPos;
                if (SpeedrunState == RunState.Running || SpeedrunState == RunState.Finish)
                {
                    GUI.Box(new Rect(labelX - 70, labelY, 2000, 2000), "");
                    GUI.Label(new Rect(labelX - 65, labelY + 5f, 2000, 2000), $"<b>{LocalizedStr("gui_run_info").ToUpper()}</b>");
                    GUI.Label(new Rect(labelX - 65, labelY + 25f, 2000, 2000), $"{_stat}");
                    GUI.Label(new Rect(labelX - 65, Screen.height - 20, 2000, 2000), _infoStat);
                }
            }
        }



        public void SaveRunTimer(SpeedrunSaveType type)
        {
            if (!SpeedrunMode.Value || IsSepeedrunsDisabled)
                return;

            float timeDif = _currentTime - _previousSaveTime;
            string timeDifTxt = ReturnTimeAsString(timeDif, false, true);
            switch (type)
            {
                case SpeedrunSaveType.Checkpt:
                    _checkpointNum += 1;
                    _stat = $"{LocalizedStr("gui_checkpoint")} №{_checkpointNum} ({timeDifTxt})\n{_stat}";
                    baseBoxPos += 15f;
                    _checkpointPopup?.SetActive(true);
                    _splitTimeText?.SetActive(true);
                    _splitTimeText?.GetComponent<TextMeshProUGUI>().SetText(_timerText);
                    break;
                case SpeedrunSaveType.Lap:
                    _lapNum += 1;
                    _stat = $"{LocalizedStr("gui_pizzatowerreference")} №{_lapNum} ({timeDifTxt})\n{_stat}";
                    baseBoxPos += 15f;
                    _checkpointPopup?.SetActive(true);
                    _splitTimeText?.SetActive(true);
                    _splitTimeText?.GetComponent<TextMeshProUGUI>().SetText(_timerText);
                    break;
                case SpeedrunSaveType.Qual:
                    _stat = $"{LocalizedStr("gui_run_qual")} {_timerText} ({timeDifTxt})\n{_stat}";
                    baseBoxPos += 15f;
                    break;
                case SpeedrunSaveType.Elim:
                    _stat = $"{LocalizedStr("gui_run_elim")} {_timerText} ({timeDifTxt})\n{_stat}";
                    baseBoxPos += 15f;
                    break;
                case SpeedrunSaveType.Win:
                    _stat = $"{LocalizedStr("gui_run_win")} {_timerText} ({timeDifTxt})\n{_stat}";
                    baseBoxPos += 15f;
                    break;
                case SpeedrunSaveType.None:
                    break;
            }

            _previousSaveTime = _currentTime;
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
            baseBoxPos = 10f;
            _currentTime = 0;
            _previousSaveTime = 0;
            _checkpointNum = 0;
            _lapNum = 0;
            _stat = string.Empty;
        }

        public IEnumerator NewRun()
        {
            HandleState(RunState.Respawned);
            FGBehaviour.FGCC.ResetToDefaultState();
            if (!RespawnAtCheckpoint.Value)
                FGBehaviour.transform.SetPositionAndRotation(_spawnPos, _spawnRot);
            else
                FGBehaviour.transform.SetPositionAndRotation(FGBehaviour.spawnpoint.transform.position, FGBehaviour.spawnpoint.transform.rotation);
            yield return new WaitForEndOfFrame();
            FGTServiceManager.GetService<RoundLoaderService>().RoundCamera.ForceRecenterToHeading();
            CheckpointManager cm = Resources.FindObjectsOfTypeAll<CheckpointManager>().FirstOrDefault();
            cm?._netIDToCheckpointMap.Clear();
            if (SPResetPoints.Value)
                CGM._soloScoreManager.SetSoloScore(FGBehaviour.FGMPG.NetID, 0);

            //FGToolsBehaviourOLD.FGBehaviour.gameObject.GetComponent<Rigidbody>().velocity = new Vector3(0, 5, 0);
        }

        public void DoRunSave()
        {
            if (!StateManager.IsFGC)
                SaveRun(LatestRunScene, _currentTime);
            else
                SaveRun(CGM._round.Id, _currentTime);
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
