using BepInEx.Logging;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using FG.Common;
using FG.Common.Audio;
using FGClient;
using FGClient.UI;
using FGTools.Services;
using FGTools.Services.Logic;
using FGTools.States.Logic;
using FMODUnity;
using Il2CppInterop.Runtime.Attributes;
using Sentry.Internal;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UniverseLib;
using UniverseLib.UI;
using static FGTools.Config.Config;
using static FGTools.Internal.Extensions.FLZ_Extensions;
using static FGTools.Services.LocalizationService;
using static FGTools.Services.OnlineCheckService;
using static FGTools.UI.FGToolsUI;


namespace FGTools.UI
{
    public class ReadyPopups : FGTBase
    {
        public static void IMG2FGCAlert()
        {
            DoModal(new(LocalizedStr("img2fgc_title"), LocalizedStr("img2fgc_desc"), UIModalMessage.ModalType.MT_OK_CANCEL, UIModalMessage.OKButtonType.Disruptive, new Action<bool>(LaunchIMG2FGC), hideLvl: ModalHideGUIType.KeepHiddenForThisModal));
            static void LaunchIMG2FGC(bool wasok)
            {
                if (wasok)
                {
                    List<string> writeInfo = new List<string>();
                    string outputfile = Path.Combine(Application.persistentDataPath, "output.txt");
                    if (File.Exists(outputfile))
                        File.Delete(outputfile);
                    File.Create(outputfile).Close();
                    writeInfo.Add("path_to_file" + " = " + Launcher.ImgDir + FGTServiceManager.GetService<MediaService>().imgPath);
                    writeInfo.Add("width" + " = " + NewGUI.Instance.imgWidth);
                    writeInfo.Add("height" + " = " + NewGUI.Instance.imgHeight);
                    writeInfo.Add("shouldDeleteBlackPixels" + " = " + NewGUI.Instance.shouldDeleteBlackPixels);
                    writeInfo.Add("shouldDeleteWhitePixels" + " = " + NewGUI.Instance.shouldDeleteWhitePixels);
                    writeInfo.Add("isDigital" + " = " + NewGUI.Instance.isDigital);
                    File.WriteAllLines(outputfile, writeInfo);
                    Application.OpenURL(Launcher.IMG2FGCExe);
                }
            }
        }

        public static void AreYouSurePopup(string action, Action<bool> popAct = null)
        {
            DoModal(new(LocalizedStr("confirm_title"), $"{LocalizedStr("confirm_desc")} {action}", UIModalMessage.ModalType.MT_OK_CANCEL, UIModalMessage.OKButtonType.Default, new Action<bool>(popAct), hideLvl: ModalHideGUIType.KeepHiddenForThisModal));
        }

        public static void ErrorPopup(object err, Action<bool> onClick = null, bool forceLeaveToMenu = false, bool displayOnlyError = true, string title = "failed_title", string desc = "failed_desc_new")
        {
            string msgAsStr = string.Empty;

            if (err is Exception fail_ex)
                msgAsStr = $"Meesage: {fail_ex.Message}\nStackTrace: {fail_ex.StackTrace}";
            else if (err is string fail_str)
                msgAsStr = fail_str;
            else
            {
                FGTLog(LogLevel.Error, "ErrorPopup()", "Not valid type = " + err.GetType().FullName);
                return;
            }

            if (forceLeaveToMenu)
            {
                onClick += new Action<bool>(val =>
                {
                    FGTRoundLoader.HideLoadingScreens();
                    if (SceneManager.GetActiveScene().name == "MainMenu")
                        GlobalGameStateClient.Instance._gameStateMachine.ReplaceCurrentState(new StateReloadingToMainMenu(GlobalGameStateClient.Instance._gameStateMachine, GlobalGameStateClient.Instance.CreateClientGameStateData()).Cast<GameStateMachine.IGameState>());
                    else
                        LeaveMatchPopupManager.Instance.OnClose(true);
                });
            }

            FGTLog(LogLevel.Error, "ErrorPopup()", $"Called popup with reason: {msgAsStr}");
            StateManager.InternalState.LatestError = msgAsStr;

            string output = $"{LocalizedStr(desc)}\n\n<size=45%>{msgAsStr}</size>\n\n{LocalizedStr("gui_error_msg_v2", [DebugUIHotkey.Value.ToString()])}";

            if (displayOnlyError)
                output = $"<size=45%>{msgAsStr}</size>";

            DoModal(new(LocalizedStr(title), output, UIModalMessage.ModalType.MT_OK, UIModalMessage.OKButtonType.Disruptive, onClick, hideLvl: ModalHideGUIType.KeepHidden));
        }

        public static void ConfigAction(bool quit = false)
        {

            string title = LocalizedStr("gui_restart_title");
            string desc = LocalizedStr("gui_restart_desc");

            if (!quit)
            {
                title = LocalizedStr("gui_reload_title");
                desc = LocalizedStr("gui_reload_desc");
            }

            void OnClickedPopUp(bool wasok)
            {
                if (wasok)
                {
                    if (quit)
                        Application.Quit();
                    else if (StateManager.FGTCurrentState != FGTStateManager.ToolsState.Menu)
                    {
                        if (StateManager.CurrentRound != null)
                            FGTServiceManager.GetService<RoundLoaderService>().LoadCMSRound(StateManager.CurrentRound.Id, LoadSceneMode.Single);
                        else
                            FGTServiceManager.GetService<RoundLoaderService>().LoadRandomCms();
                    }
                }
            }

            DoModal(new(title, desc, UIModalMessage.ModalType.MT_OK_CANCEL, UIModalMessage.OKButtonType.Disruptive, new Action<bool>(OnClickedPopUp), hideLvl: ModalHideGUIType.ShowOnCancel));
        }

        public static void SpeedrunContinePopup(float bestTime, bool fromWinScreen = false, VictoryScreenViewModel player = null)
        {
            var sps = FGTServiceManager.GetService<SpeedrunService>();
            var noTime = "--:--<size=80%>.--</size>";

            if (bestTime != -1)
                sps.SetLatestSceneTime(bestTime);
            else
                sps.SetLatestSceneTime(0);

            float diff = FGTServiceManager.GetService<SpeedrunService>().ReturnCurrentTime() - bestTime;
            string latest;

            if (!StateManager.IsFGC)
            {
                if (sps.GetRunTime(sps.ReturnLatestScene()) == -1)
                    latest = noTime;
                else
                    latest = sps.ReturnTimeAsString(sps.GetRunTime(sps.ReturnLatestScene()), false, true, true);
            }
            else
            {
                if (sps.GetRunTime(CGM._round.Id) == -1)
                    latest = noTime;
                else
                    latest = sps.ReturnTimeAsString(sps.GetRunTime(CGM._round.Id), false, true, true);
            }

            var msg = $"{LocalizedStr("spqual_desc")}\n\n{LocalizedStr("gui_best_time")}: {latest} | {LocalizedStr("gui_run_info")}: {sps.ReturnTimeAsString(FGTServiceManager.GetService<SpeedrunService>().ReturnCurrentTime(), false, true, true)} ({FGTServiceManager.GetService<SpeedrunService>().CreateSplitTimeText(diff, diff > 0)})";

            DoModal(new(LocalizedStr("spqual_title"), msg, UIModalMessage.ModalType.MT_OK_CANCEL, UIModalMessage.OKButtonType.Positive, new Action<bool>((bool wasok) =>
            {
                if (wasok)
                    FGTServiceManager.GetService<SpeedrunService>().DoRunSave();
                bool allowRand;

                if (!fromWinScreen)
                    allowRand = QualLevel.Value == QualType.LoadRandomRoundAfter;
                else
                    allowRand = WinLevel.Value == WinType.LoadRandomRoundAfter;

                if (allowRand)
                    SpeedrunRestart();
                else
                {
                    player?.StopMusicImmediately();
                    FGTServiceManager.GetService<SpeedrunService>().HandleState(SpeedrunService.RunState.Inactive);
                    AudioMixing.Instance.ResetAllSnapshotParams();
                }
            }), hideLvl: ModalHideGUIType.KeepHidden));
        }

        public static void SpeedrunRestart()
        {
            DoModal(new(LocalizedStr("spqual_title2"), LocalizedStr("spqual_desc3"), UIModalMessage.ModalType.MT_OK_CANCEL, UIModalMessage.OKButtonType.Positive, new Action<bool>(wasok =>
            {
                if (wasok)
                {
                    FGTServiceManager.GetService<SpeedrunService>().EndCurrentRun();
                    if (SceneManager.GetActiveScene().name == CGM._round.GetSceneName() && !OldSPContinue.Value)
                        FGBehaviour.ReturnToStart();
                    else
                        FGTServiceManager.GetService<RoundLoaderService>().LoadCMSRound(StateManager.CurrentRound.Id, LoadSceneMode.Single);
                }
                else
                    FGTServiceManager.GetService<RoundLoaderService>().LoadRandomCms();
            }), hideLvl: ModalHideGUIType.KeepHidden));
        }

        public static void AlertSpeedrunner()
        {
            AddCMSString("speedrunneralert_true", $"{LocalizedStr("speedrunneralert_accept")}");
            AddCMSString("speedrunneralert_false", $"{LocalizedStr("speedrunneralert_cancel")}");

            var ModalMessageDataDisclaimer = new ModalMessageData
            {
                Title = LocalizedStr("speedrunneralert_title"),
                Message = LocalizedStr("speedrunneralert_desc", [CleanStr(StateManager.CurrentRound.DisplayName.Text)]),
                LocaliseTitle = UIModalMessage.LocaliseOption.NotLocalised,
                LocaliseMessage = UIModalMessage.LocaliseOption.NotLocalised,
                ModalType = UIModalMessage.ModalType.MT_OK_CANCEL,
                OkButtonType = UIModalMessage.OKButtonType.Positive,
                OnCloseButtonPressed = new Action<bool>(val =>
                {
                    if (val)
                        LeaveMatchPopupManager.Instance.OnClose(true);
                }),
                OkTextOverrideId = "speedrunneralert_true",
                CancelTextOverrideId = "speedrunneralert_false",
            };

            PopupManager.Instance.Show(PopupInteractionType.Error, ModalMessageDataDisclaimer);
            AudioManager.PlayOneShot(AudioManager.EventMasterData.GenericPopUpAppears);
            GameObject btns = GameObject.Find("ButtonContainer");
            if (btns != null)
            {
                btns.SetActive(false);

                [HideFromIl2Cpp]
                IEnumerator returnBtns()
                {
                    yield return new WaitForSeconds(3);
                    AudioManager.PlayOneShot(AudioManager.EventMasterData.QualificationConfetti);
                    btns.SetActive(true);
                }
                CoroutineRunner.Instance.StartCoroutine(returnBtns().WrapToIl2Cpp());
            }
        }
    }
}
