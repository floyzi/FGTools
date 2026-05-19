using System;
using System.IO;
using BepInEx.Logging;
using FGClient;
using FGTools.Services;
using FGTools.States;
using FGTools.States.Logic;
using NAudio.Wave;
using UnityEngine;
using static FGTools.Internal.Extensions.FLZ_Extensions;

namespace FGTools.Internal.Behaviours
{
    internal class MenuAudioProvider : ToolsBehaviour
    {
        public static MenuAudioProvider instance;
        public bool stop = false;
        public bool intro = true;
        public WaveOutEvent introEvent = null;
        public WaveOutEvent loopEvent = null;

        WaveStream loopWaveProvider;
        VolumeWaveProvider16 loopVolumeWaveProvider;
        double targetTime = -1;
        public double elapsedTime;
        string input;
        float cutoffEdit;
        string cutoffEditHandler;
        float introEdit;
        string introEditHandler;
        string debugOut;
        float guiPosBase = 280;
        float musVol = 0.6f;
        float fadeTime = 0.8f;
        float targetMusVol = 0.6f;
        public bool needToFadeIn;
        public bool needToFadeOut;
        bool gui = true;
        bool focusPending = false;

        private void Awake()
        {
            if (instance == null)
                instance = this;
            else
                Destroy(instance);
        }

        public void OnDestroy()
        {
            StopMusic();
        }

#if AUDIODEBUG
        public void OnGUI()
        {

            if (gui)
            {
                GUI.Box(new Rect(10f, guiPosBase + 10, 250f, 332f), "");
                GUI.Label(new Rect(15f, guiPosBase + 15, 255f, 30f), "MenuAudioDebug - F5 toggle");

                if (targetTime != -1)
                {
                    input = GUI.TextField(new Rect(15f, guiPosBase + 40, 240f, 20f), input);
                    if (GUI.Button(new Rect(15f, guiPosBase + 60, 240f, 20f), "set position"))
                    {
                        var pos = float.Parse(input);
                        loopWaveProvider.CurrentTime = TimeSpan.FromSeconds(pos);
                        elapsedTime = pos;
                    }
                    cutoffEditHandler = GUI.TextField(new Rect(15f, guiPosBase + 80, 240f, 20f), cutoffEditHandler);
                    if (GUI.Button(new Rect(15f, guiPosBase + 100, 240f, 20f), "set cutoff"))
                    {
                        cutoffEdit = float.Parse(cutoffEditHandler);
                        targetTime = loopWaveProvider.TotalTime.TotalSeconds - /*ThemesLogic.selectedTheme.EndCutoff*/ cutoffEdit;
                    }

                    GUI.Label(new Rect(15f, guiPosBase + 120, 240f, 50f), debugOut);
                    GUI.Label(new Rect(15f, guiPosBase + 160, 240f, 50f), $"Intro: {FGTServiceManager.GetService<MenuThemeService>().CurrentTheme.IntroLength} Custom: {introEdit} Cutoff: {FGTServiceManager.GetService<MenuThemeService>().CurrentTheme.EndCutoff} Custom: {cutoffEdit} CurrentVol: {musVol} TargetVol: {targetMusVol}");

                    if (GUI.Button(new Rect(15f, guiPosBase + 200, 240f, 20f), "toggle volume"))
                        toggleMute(false);

                    introEditHandler = GUI.TextField(new Rect(15f, guiPosBase + 220, 240f, 20f), introEditHandler);
                    if (GUI.Button(new Rect(15f, guiPosBase + 240, 240f, 20f), "set intro"))
                    {
                        introEdit = float.Parse(introEditHandler);

                    }
                    if (GUI.Button(new Rect(15f, guiPosBase + 260, 240f, 20f), "fadein"))
                    {
                        needToFadeIn = true;
                    }
                    if (GUI.Button(new Rect(15f, guiPosBase + 280, 240f, 20f), "fadeout"))
                    {
                        needToFadeOut = true;
                    }
                    GUI.Toggle(new Rect(15f, guiPosBase + 300, 240, 20), needToFadeIn, "fadein");
                    GUI.Toggle(new Rect(65f, guiPosBase + 300, 240, 20), needToFadeOut, "fadeout");
                    GUI.Toggle(new Rect(125f, guiPosBase + 300, 240, 20), muted, "muted");
                    GUI.Toggle(new Rect(175f, guiPosBase + 300, 240, 20), celebrationPreview, "celebView");

                }
                else
                    GUI.Label(new Rect(15f, guiPosBase + 40, 255f, 30f), "targetTime = -1");

                GUI.Toggle(new Rect(15f, guiPosBase + 320, 240, 20), focusPending, "focusPending");
            }
        }
#endif
        public void PlayMusic(bool force)
        {
            if (string.IsNullOrEmpty(FGTServiceManager.GetService<MenuThemeService>().CurrentThemePath))
                return;

            var ms = StateManager.GetState<MenuState>();
            if (ms != null && ms.IntroStopwatch.IsRunning)
                return;

            if (Application.isFocused)
            {
#if AUDIODEBUG
                cutoffEdit = FGTServiceManager.GetService<MenuThemeService>().CurrentTheme.EndCutoff;
                introEdit = FGTServiceManager.GetService<MenuThemeService>().CurrentTheme.IntroLength;
#endif
                string loopPath = $"{Launcher.ThemesDir}{Path.GetDirectoryName(FGTServiceManager.GetService<MenuThemeService>().CurrentThemePath)}\\{FGTServiceManager.GetService<MenuThemeService>().CurrentTheme.LoopMusic}";
                StopMusic(false);

                if (File.Exists(loopPath) && (force || loopEvent == null))
                {
                    FGTLog(LogLevel.Info, base.GetType(), $"PlayMusic(): force: {force}, fileName {Path.GetFileName(loopPath)}");

                    loopEvent = new WaveOutEvent();
                    loopWaveProvider = Path.GetExtension(loopPath) == ".wav" ? new WaveFileReader(loopPath) : new Mp3FileReader(loopPath);
                    loopVolumeWaveProvider = new VolumeWaveProvider16(loopWaveProvider)
                    {
                        Volume = 0
                    };

                    stop = false;
                    loopEvent?.Init(loopVolumeWaveProvider);
#if AUDIODEBUG
                    targetTime = loopWaveProvider.TotalTime.TotalSeconds - cutoffEdit;
#else
                    targetTime = loopWaveProvider.TotalTime.TotalSeconds - FGTServiceManager.GetService<MenuThemeService>().CurrentTheme.EndCutoff;
#endif
                    loopEvent?.Play();
                }
            }
            else
            {
                FGTLog(LogLevel.Info, base.GetType(), $"PlayMusic(): waiting for game to focus...");
                focusPending = true;
            }

        }

        public void StopMusic(bool fadeout = false)
        {
            if (loopEvent != null && loopWaveProvider != null)
            {
                loopEvent.Stop();
                loopWaveProvider.Dispose();
                loopEvent.Dispose();
                targetTime = -1;
                elapsedTime = 0;
                stop = true;
                loopEvent = null;
                loopWaveProvider = null;
                FGTLog(LogLevel.Info, base.GetType(), "StopMusic(): Music was stopped");

                GC.Collect();
            }
            else
            {
                FGTLog(LogLevel.Info, base.GetType(), $"StopMusic(): Music can't be stopped as it not playing | {loopEvent != null} {loopWaveProvider != null}");
            }
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.F5))
                gui = !gui;

            if (Application.isFocused && focusPending)
            {
                focusPending = false;
                PlayMusic(true);
            }
            if (FGTServiceManager.GetService<MenuThemeService>().CurrentTheme != null)
            {
                if (!muted && !needToFadeIn && !needToFadeOut && musVol == targetMusVol)
                    musVol = GlobalGameStateClient.Instance.PlayerProfile.AudioSettings.MusicVolume * GlobalGameStateClient.Instance.PlayerProfile.AudioSettings.MasterVolume * FGTServiceManager.GetService<MenuThemeService>().CurrentTheme.VolumeModifier;
                targetMusVol = GlobalGameStateClient.Instance.PlayerProfile.AudioSettings.MusicVolume * GlobalGameStateClient.Instance.PlayerProfile.AudioSettings.MasterVolume * FGTServiceManager.GetService<MenuThemeService>().CurrentTheme.VolumeModifier;


                if (!muted)
                {
                    if (musVol > 0 && needToFadeOut)
                    {
                        if (needToFadeIn)
                        {
                            musVol = targetMusVol;
                            needToFadeIn = false;
                        }
                        musVol = Mathf.MoveTowards(musVol, 0, targetMusVol / fadeTime * Time.deltaTime);
                        if (musVol <= 0)
                            needToFadeOut = false;
                    }

                    if (musVol <= targetMusVol && needToFadeIn)
                    {
                        if (needToFadeOut)
                        {
                            musVol = 0;
                            needToFadeOut = false;
                        }
                        musVol = Mathf.MoveTowards(musVol, targetMusVol, targetMusVol / fadeTime * Time.deltaTime);
                        if (musVol >= targetMusVol)
                            needToFadeIn = false;
                    }
                }
                if (targetTime != -1)
                {
                    loopVolumeWaveProvider.Volume = musVol;
                    elapsedTime += Time.unscaledDeltaTime;
#if AUDIODEBUG
                    debugOut = $"current time: {elapsedTime:F4} - target time: {targetTime:F4}";
#endif
                    if (elapsedTime >= targetTime)
                    {
#if AUDIODEBUG
                        loopWaveProvider.CurrentTime = TimeSpan.FromSeconds(introEdit);
                        elapsedTime = introEdit;
#else
                            loopWaveProvider.CurrentTime = TimeSpan.FromSeconds(FGTServiceManager.GetService<MenuThemeService>().ThemeOnPreview.IntroLength);
                            elapsedTime = FGTServiceManager.GetService<MenuThemeService>().ThemeOnPreview.IntroLength;
#endif
                    }
                }
            }
        }

        bool muted = false;
        public bool celebrationPreview = false;

        void toggleMute(bool fade)
        {
            if (fade)
            {
                if (musVol == 0)
                    needToFadeIn = true;
                else if (musVol == targetMusVol)
                    needToFadeOut = true;

                return;
            }

            if (!muted)
            {
                musVol = 0;
                muted = true;
                return;
            }

            if (muted)
            {
                muted = false;
                musVol = targetMusVol;
            }
        }

        void OnApplicationFocus(bool hasFocus)
        {
            if (loopVolumeWaveProvider != null && AudioManager.Instance.MuteAudioOnFocusLostSetting && !celebrationPreview)
                toggleMute(false);
        }
    }
}
