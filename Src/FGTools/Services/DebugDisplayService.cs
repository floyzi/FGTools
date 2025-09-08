using BepInEx.Unity.IL2CPP.Utils.Collections;
using FG.Common;
using FGTools.Config;
using FGTools.Internal.Behaviours;
using FGTools.Services.Logic;
using FGTools.States.Logic;
using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using static FGTools.Config.ConfigManager;
using static FGTools.Services.LocalizationService;

namespace FGTools.Services
{
    internal class DebugDisplayService : FGTService
    {
        const float InfoDisplayTime = 5f;
        const float PendingTime = 3f;
        KeyCode ExpandToggle = KeyCode.LeftArrow;
        readonly string[] DebugContent = new string[5];
        bool UIToggle = false;
        readonly float FontSize = 0.0123f;

        bool _displaySaveResult = false;
        float _time = 0;
        float _width = 320f;
        bool _expanded = false;
        string _expandTxt = "<-";
        bool _needSecondPress = false;

        string ReturnDebugInfo()
        {
            var output = new StringBuilder();

            output.AppendLine($"<b>{LocalizedStr("debug_title")} - {LocalizedStr("gui_debug_hide", [DebugUIHotkey.Value])}</b>\n");
            output.AppendLine($"<b>— INFO</b>");
            output.AppendLine($"{DebugContent[0]}");
            output.AppendLine($"<b>— STATE</b>");
            output.AppendLine($"{DebugContent[1]}");
            output.AppendLine($"<b>— FALL GUYS</b>");
            output.AppendLine($"{DebugContent[2]}");
            output.AppendLine($"<b>— ONLINE CHECK</b>");
            output.AppendLine($"{DebugContent[3]}");

            if (CGM != null)
            {
                output.AppendLine($"<b>— GAME MANAGER</b>");
                output.AppendLine($"{DebugContent[4]}");
            }

            return output.ToString();
        }

        static string ReturnException(Exception e) => $"Unable to display debug info!<color=grey>\n\n{e.Message}\n\n{e.StackTrace}</color>\n\nCapture bug report by pressing \"{LocalizedStr("gui_debug_new_report")}\"";
        public override void DrawGUI()
        {
            if (_needSecondPress)
            {
                var label = $"<b>{LocalizedStr("gui_debug_press_again", [$"[{ DebugUIHotkey.Value}]"]).ToUpper()}</b>";
                var labSize = GUI.skin.label.CalcSize(new(label));
                GUI.Box(new Rect(Screen.width - labSize.x - 20, 0, labSize.x + 20, labSize.y + 5), label);
            }

            if (!UIToggle)
                return;

            GUIStyle debugStyle = new()
            {
                fontSize = (int)(FontSize * Screen.height),
            };
            debugStyle.normal.textColor = Color.white;

            var rect = new Rect(Screen.width - _width, 0, _width, Screen.height + 25f);

            GUI.Box(rect, "");
            GUI.Box(rect, "");
    
            if (GUI.Button(new Rect(Screen.width - 30, 0, 30, 20), _expandTxt))
            {
                if (_expanded)
                    ToggleExpand(KeyCode.RightArrow);
                else
                    ToggleExpand(KeyCode.LeftArrow);
            }

            var bottomLine = $"<size=10>{LocalizedStr("gui_debug_new_report_desc")}</size>";
            var defRound = StateManager.PreviousRound == null ? "null" : $"{StateManager.PreviousRound.Id}";
            var curRound = StateManager.CurrentRound == null ? "null" : $"{StateManager.CurrentRound.Id}";

            try
            {
                var b1 = new StringBuilder();

                b1.AppendLine($"Build: {Launcher.BuildInfo.BuildDate}");
                b1.AppendLine($"Ver: {Launcher.BuildInfo.UI_Version} | BuildEnv: {Launcher.BuildInfo.Config} | Commit: #{Launcher.BuildInfo.GetCommit()}");
                b1.AppendLine($"Build ID: {Launcher.BuildInfo.GUID}");
                b1.AppendLine($"Session Length: {DateTime.UtcNow.Subtract(Launcher.StartupTime):hh\\:mm\\:ss}");
                b1.AppendLine($"Locale: {ConfigManager.LangFileName.Value}");
                b1.AppendLine($"CanUseHotkeys: {StateManager.CanUseHotkeys}");
                b1.AppendLine($"HarmonyPatched: {Launcher.HarmonyPatched}");
                b1.AppendLine($"AdditiveLoad: {FGTServiceManager.GetService<RoundLoaderService>().UsingAdditiveLoad}");
                b1.AppendLine($"CurrentRoundID: {curRound}");
                b1.AppendLine($"PreviousRoundID: {defRound}");
                b1.AppendLine($"OfflinePatches: {StateManager.InternalState.OfflinePatches}");
                b1.AppendLine($"FGCPatches: {Launcher.FGCHarmonyPatched}");
                b1.AppendLine($"AllCosmetics: {ConfigManager.AllCosmetics.Value}");
                b1.AppendLine($"SelectedTheme: {ConfigManager.InGameTheme.Value}");
                b1.AppendLine($"DiscordRpc: {ConfigManager.AllowRPC.Value}");
                b1.AppendLine($"{FGTServiceManager.ReturnDebugInfo()}");

                DebugContent[0] = b1.ToString();
            }
            catch (Exception e)
            {
                DebugContent[0] = ReturnException(e);
            }

            try
            {
                var b2 = new StringBuilder();

                b2.AppendLine($"FGTState: {StateManager.ActiveState}");
                b2.AppendLine($"TimeInState: {StateManager.TimeInState}");
                b2.AppendLine($"FGTStateEnum: {StateManager.FGTCurrentState}");
                b2.AppendLine($"FGState: {StateManager.FGCurrentState}");
                b2.AppendLine($"SPState: {FGTServiceManager.GetService<SpeedrunService>().SpeedrunState}");
                b2.AppendLine($"RPCState: {FGTServiceManager.GetService<DiscordRPCService>().State}");
                b2.AppendLine($"IsFGC: {StateManager.IsFGC}");
                b2.AppendLine($"IsGameplay: {StateManager.IsInGameplay}");
                b2.AppendLine($"IsExploreFGC: {StateManager.IsPlayingExplore}");

                DebugContent[1] = b2.ToString();
            }
            catch (Exception e)
            {
                DebugContent[1] = ReturnException(e);
            }

            try
            {
                var b3 = new StringBuilder();

                b3.AppendLine($"Client: {ClientBuildDetails.Platform} | {ClientBuildDetails.AppVersion} | {ClientBuildDetails.PlatformServiceProvider} | {Application.unityVersion}");
                b3.AppendLine($"MEM: {StateManager.MemUsage:F2} MB | PEAK: {StateManager.PeakMemUsage:F2} MB");
                b3.AppendLine($"Scene: {SceneManager.GetActiveScene().name}");
                b3.AppendLine($"NetworkOptions: {NetworkGameData.currentGameOptions_._roundID}");
                b3.AppendLine($"IsGameDead: {true}");

                DebugContent[2] = b3.ToString();
            }
            catch (Exception e)
            {
                DebugContent[2] = ReturnException(e);
            }

            try
            {
                DebugContent[3] = $"{FGTServiceManager.GetService<OnlineCheckService>().ReturnDebugInfo()}";
            }
            catch (Exception e)
            {
                DebugContent[3] = ReturnException(e);
            }

            try
            {
                if (CGM != null && CGM.CurrentGameSession != null)
                {
                    var b4 = new StringBuilder();

                    b4.AppendLine($"Session State: {CGM.CurrentGameSession.CurrentSessionState} ({CGM.CurrentGameSession.TimeInState})");
                    //b4.AppendLine($"Time Remain: {StateManager.CGM.CurrentGameSession.GamePlayingTimeRemaining}");
                    b4.AppendLine($"Readiness State: {CGM._readinessState}");
                    //b4.AppendLine($"Shutdown: {StateManager.CGM.IsShutdown}");
                    b4.AppendLine($"Round Time: {CGM.CurrentGameSession.SimulationTimeRound} / {CGM.CurrentGameSession.EndRoundTime}"); 

                    DebugContent[4] = b4.ToString();
                }
                else
                    DebugContent[4] = string.Empty;
            }
            catch (Exception e)
            {
                DebugContent[4] = ReturnException(e);
            }

            if (_displaySaveResult)
            {
                if (_time < InfoDisplayTime)
                {
                    bottomLine = $"{LocalizedStr("gui_debug_new_report_success")}";
                    _time += Time.unscaledDeltaTime;
                }
                else
                {
                    _displaySaveResult = false;
                    _time = 0;
                }
            }

            GUI.Label(new Rect(Screen.width - _width + 10, 5, _width, Screen.height - 5f), $"{ReturnDebugInfo()}", debugStyle);
            GUI.Label(new Rect(Screen.width - _width + 10, Screen.height - 65, _width, 45), bottomLine);

            float buttonWidth = (_width - 30f) / 2;

            createAsZip = GUI.Toggle(new Rect(Screen.width - _width + 10, Screen.height - 45, buttonWidth, 20), createAsZip, LocalizedStr("gui_debug_as_zip"));

            if (GUI.Button(new Rect(Screen.width - _width + 10, Screen.height - 25, buttonWidth, 20), LocalizedStr("gui_debug_new_report")))
            {
                CoroutineRunner.Instance.StartCoroutine(CaptureReport(ReturnDebugInfo()).WrapToIl2Cpp());
            }
            if (GUI.Button(new Rect(Screen.width - _width / 2 + 5, Screen.height - 25, buttonWidth - 0, 20), LocalizedStr("gui_debug_report_folder")))
            {
                Application.OpenURL(Launcher.BugReportsDir);
            }
        }

        bool createAsZip = false;

        IEnumerator CaptureReport(string debug)
        {
            if (!_displaySaveResult)
            {
                foreach (string file in Directory.GetFiles(Launcher.BugReportsDir))
                {
                    if (file.Contains("_LATEST"))
                        File.Move(file, file.Split("_LATEST")[0] + "." + file.Split('.')[1]);
                }

                foreach (string dir in Directory.GetDirectories(Launcher.BugReportsDir))
                {
                    if (dir.Contains("_LATEST"))
                        Directory.Move(dir, dir.Split("_LATEST")[0]);
                }

                string newReportName = $"Report_{DateTime.UtcNow:HHMMssFF}_LATEST";
                string newReportDir = Path.Combine(Launcher.BugReportsDir, newReportName);
                Directory.CreateDirectory(newReportDir);

                ScreenCapture.CaptureScreenshot(Path.Combine(newReportDir, "Screenshot.png"));
                yield return new WaitForSeconds(0.1f);

                string log = Application.persistentDataPath + "\\Player.log";
                using (StreamReader reader = new(File.Open(log, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
                {
                    File.WriteAllText(Path.Combine(newReportDir, $"Player.log"), reader.ReadToEnd());
                }

                string bepinlog = Path.Combine(BepInEx.Paths.BepInExRootPath, "LogOutput.log");
                using (StreamReader reader = new(File.Open(bepinlog, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
                {
                    File.WriteAllText(Path.Combine(newReportDir, $"LogOutput.log"), reader.ReadToEnd());
                }

                string bepinerrorlog = Path.Combine(BepInEx.Paths.BepInExRootPath, "ErrorLog.log");
                using (StreamReader reader = new(File.Open(bepinerrorlog, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
                {
                    File.WriteAllText(Path.Combine(newReportDir, $"ErrorLog.log"), reader.ReadToEnd());
                }

                string сonfig = Path.Combine(BepInEx.Paths.ConfigPath, "flz.fgt.cfg");
                using (StreamReader reader = new(File.Open(сonfig, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
                {
                    File.WriteAllText(Path.Combine(newReportDir, $"flz.fgt.cfg"), reader.ReadToEnd());
                }

                File.WriteAllText(Path.Combine(newReportDir, $"DebugInfo.txt"), debug);
                if (createAsZip)
                {
                    System.IO.Compression.ZipFile.CreateFromDirectory(newReportDir, Launcher.BugReportsDir + "\\" + newReportName + ".zip");
                    foreach (string file in Directory.GetFiles(newReportDir))
                        File.Delete(file);
                    Directory.Delete(newReportDir);
                }
                _displaySaveResult = true;
            }
        }

        public override void RegisterService()
        {
        }

        void ToggleExpand(KeyCode key)
        {
            if (key == KeyCode.LeftArrow)
            {
                _width = 950;
                _expandTxt = "->";
                ExpandToggle = KeyCode.RightArrow;
                _expanded = true;
            }
            else
            {
                _width = 320;
                _expandTxt = "<-";
                ExpandToggle = KeyCode.LeftArrow;
                _expanded = false;
            }
        }

        public override void UpdateService()
        {
            if (_needSecondPress)
            {
                _time += Time.unscaledDeltaTime;
                if (_time >= PendingTime)
                {
                    _needSecondPress = false;
                    _time = 0;
                }
            }
            if (Input.GetKeyDown(DebugUIHotkey.Value))
            {
                if (UIToggle)
                {
                    UIToggle = false;
                    return;
                }    
                if (!_needSecondPress)
                    _needSecondPress = true;
                else
                {
                    _time = 0;
                    _needSecondPress = false;
                    UIToggle = !UIToggle;
                }
                //SRDebug.Instance.ShowDebugPanel(true);
            }

            if (UIToggle)
            {
                if (Input.GetKeyDown(ExpandToggle))
                    ToggleExpand(ExpandToggle);
            }
        }
    }
}
