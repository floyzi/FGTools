using BepInEx.Configuration;
using BepInEx.Logging;
using FGTools.Services;
using FGTools.States.Logic;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using UnityEngine;
using static FGTools.Internal.Extensions.FLZ_Extensions;
using static FGTools.UI.ReadyPopups;
using KeyCode = UnityEngine.KeyCode;

namespace FGTools.Config
{
    //TODO: rework this
    public class Config : FGTBase
    {
        public static ConfigFile CFG;

        const string OptionsSect = "FGTOOLS OPTIONS";
        const string HotkeysUISect = "HOTKEYS - UI";
        const string HotkeysGPSect = "HOTKEYS - GAMEPLAY";
        const string HotkeysFFMSect = "HOTKEYS - FREE FLY MODE";
        const string HotkeysFCSect = "HOTKEYS - FREE CAMERA";
        const string AllCosmeticsSect = "ALL COSMETICS";
        const string FFMSect = "FREE FLY MODE";
        const string FCSect = "FREE CAMERA";
        const string WatermarkSect = "WATERMARK";
        const string GPSect = "GAMEPLAY";
        const string CPSect = "CHARACTER PHYSICS";
        const string SPSect = "SPEEDRUNNING";
        const string PWSect = "POWERUP SETTINGS";
        const string FGCSect = "FG CREATIVE";

        public enum Watermark
        {
            None,
            OnlyVersion,
            VersionAndCredits,
        }

        public enum QualType
        {
            None,
            LoadRandomRoundAfter,
        }

        public enum ElimType
        {
            None,
            LoadRandomRoundAfter,
        }
        public enum WinType
        {
            None,
            LoadRandomRoundAfter,
        }

        public enum RandomRoundsFilter
        {
            All,
            Race,
            Logic,
            Survival,
            Final,
            Team,
            TimeAttack,
            Hunt,
        }

        public enum SelectedPowerup
        {
            None,
            RollingBall,
            Invisibeans,
            ExplodingRhino,
            RubberChicken
        }

        public enum MirrorType
        {
            Auto,
            GitHub,
            Netlify,
            Vercel,
            Cloudflare,
            MyCDN,
        }

        //LOADER OPTIONS
        public static ConfigEntry<string> LangFileName { get; set; }
        public static ConfigEntry<bool> UseBackupLocale { get; set; }
        public static ConfigEntry<bool> AllowRPC { get; set; }
        public static ConfigEntry<string> InGameTheme { get; set; }
        public static ConfigEntry<bool> AutoSetPreset { get; set; }

        #region HOTKEYS BINDINGS
        public static ConfigEntry<KeyCode> ToggleCusorHotkey { get; set; }
        public static ConfigEntry<KeyCode> ToggleUIHotkey { get; set; }
        public static ConfigEntry<KeyCode> ToggleFreeCamHotkey { get; set; }
        public static ConfigEntry<KeyCode> PauseFreeCamHotkey { get; set; }
        public static ConfigEntry<KeyCode> RespawnHotkey { get; set; }
        public static ConfigEntry<KeyCode> CheckpointHotkey { get; set; }
        public static ConfigEntry<KeyCode> FreeCamMoveUpHotkey { get; set; }
        public static ConfigEntry<KeyCode> FreeCamMoveDownHotkey { get; set; }
        public static ConfigEntry<KeyCode> RestartRunHotkey { get; set; }
        public static ConfigEntry<KeyCode> ResetCheckpointHotkey { get; set; }
        public static ConfigEntry<KeyCode> SkipIntroHotkey { get; set; }
        public static ConfigEntry<KeyCode> DebugUIHotkey { get; set; }
        public static ConfigEntry<KeyCode> FreeCamToggleUI { get; set; }
        public static ConfigEntry<KeyCode> EnterFFM { get; set; }
        public static ConfigEntry<KeyCode> MoveUP { get; set; }
        public static ConfigEntry<KeyCode> MoveDOWN { get; set; }
        public static ConfigEntry<KeyCode> MoveFORWARD { get; set; }
        public static ConfigEntry<KeyCode> MoveBACKWARD { get; set; }
        public static ConfigEntry<KeyCode> MoveLEFT { get; set; }
        public static ConfigEntry<KeyCode> MoveRIGHT { get; set; }
        #endregion


        //ALL COSMETICS
        public static ConfigEntry<bool> AllCosmetics { get; set; }
        public static ConfigEntry<bool> AllCosmeticsAlert { get; set; }

        //FFM
        public static ConfigEntry<float> FFMSpeedH { get; set; }
        public static ConfigEntry<float> FFMSpeedV { get; set; }

        //FREECAM
        public static ConfigEntry<float> FreeCamSpeed { get; set; }
        public static ConfigEntry<float> FreeCamSens { get; set; }
        public static ConfigEntry<float> FreeCamZoomSpeed { get; set; }
        public static ConfigEntry<bool> FreeCamAudioEffect { get; set; }

        //WATERMARK
        public static ConfigEntry<Watermark> WatermarkLevel { get; set; }

        //GAMEPLAY OPTIONS
        public static ConfigEntry<QualType> QualLevel { get; set; }
        public static ConfigEntry<ElimType> ElimLevel { get; set; }
        public static ConfigEntry<WinType> WinLevel { get; set; }
        public static ConfigEntry<bool> GravZoneEffect { get; set; }
        public static ConfigEntry<bool> InvisibleCheckpoint { get; set; }
        public static ConfigEntry<bool> RealHardMode { get; set; }
        public static ConfigEntry<float> RCDelay { get; set; }
        public static ConfigEntry<float> SkipIntroTime { get; set; }
        public static ConfigEntry<bool> ColliderView { get; set; }
        public static ConfigEntry<bool> RandomizeRings { get; set; }
        public static ConfigEntry<float> CameraDistance { get; set; }
        public static ConfigEntry<bool> RandomMusic { get; set; }
        public static ConfigEntry<bool> FastLoad { get; set; }

        //PHYSICS
        public static ConfigEntry<float> DiveSens { get; set; }
        public static ConfigEntry<bool> OldPhysics { get; set; }

        //SPEEDRUN MODE
        public static ConfigEntry<bool> SpeedrunMode { get; set; }
        public static ConfigEntry<float> SPRespawnCD { get; set; }
        public static ConfigEntry<bool> SpeedrunUI { get; set; }
        public static ConfigEntry<bool> SPResetPoints { get; set; }
        public static ConfigEntry<bool> OldSPContinue { get; set; }
        public static ConfigEntry<bool> SPInstaStart { get; set; }
        public static ConfigEntry<bool> RespawnAtCheckpoint { get; set; }

        //RANDOM ROUNDS
        public static ConfigEntry<RandomRoundsFilter> RoundsFilter { get; set; }

        //POWERUPS
        public static ConfigEntry<SelectedPowerup> Powerup { get; set; }
        public static ConfigEntry<bool> PowerupInventory { get; set; }
        public static ConfigEntry<bool> InfPowerups { get; set; }
        public static ConfigEntry<float> PowerupLength { get; set; }
        public static ConfigEntry<byte> PowerupAmount { get; set; }

        //FG CREATIVE
        public static ConfigEntry<bool> AllowFGCAutosaves { get; set; }
        public static ConfigEntry<float> TargetFGCSaveTime { get; set; }
        public static ConfigEntry<bool> ShowAutosaveTimer { get; set; }
        public static ConfigEntry<bool> EnableLocalAutosaves { get; set; }
        public static ConfigEntry<bool> PauseTimerExplore { get; set; }

        //SOURCES
        public static ConfigEntry<MirrorType> ContentMirror { get; set; }
        public static ConfigEntry<string> ContentSourceOverride { get; set; }

        static Dictionary<string, string> Descs = [];

        static string GetDesc(string key)
        {
            if (Descs.TryGetValue(key, out string value))
                return value;

            return $"Missing: {key}";
        }

        public static void LoadCFG(ConfigFile bepCfg)
        {
            CFG = bepCfg;
            FGTLog(LogLevel.Info, "LoadCFG", "Started parsing config...");

            Descs = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(Launcher.StaticConfigDescs));

            #region DEFAULT
            LangFileName = CFG.Bind(OptionsSect, "Localization File Name", "en", "Name of folder that will be used for FGTools localization");
            LangFileName.SettingChanged += (sender, args) => {
                //ConfigAction(true);
            };
            UseBackupLocale = CFG.Bind(OptionsSect, "Use Backup Localization", false, "Useful for people who translating FGTools to other languages. If enabled localization from server will be bypassed.");
            //offlineMode = cfg.Bind(optionsSect, "Offline Mode", false, GetDesc("loader01"));
            //offlineMode.SettingChanged += (sender, args) => {
            //    if (StateManager.FGTCurrentState == FGTStateEnum.BeforeMenu)
            //        ConfigAction();
            //    else
            //        ConfigAction(true);
            //};
            //offlineUsername = cfg.Bind(optionsSect, "Offline Name", "DEFAULT", GetDesc("loader02"));
            AllowRPC = CFG.Bind(OptionsSect, "Discord RPC", true, GetDesc("allow_rpc"));
            AllowRPC.SettingChanged += (sender, args) => {
                //if (rpcState == FGTRpcState.Disabled)
                //    rpcState = FGTRpcState.Unknown;
                //if (AllowRPC.Value)
                //   ServiceManagerFGT.GetService<DiscordRPCService>().HandleRPCState(FGTRpcState.MenuLoading);
                //else if (_Discord != null)
                //{
                //    _Discord.Dispose();
                //    _Discord = null;
                //}
            };
            InGameTheme = CFG.Bind(OptionsSect, "Theme", "DEFAULT", GetDesc("menu_theme"));
            AutoSetPreset = CFG.Bind(OptionsSect, "Auto Set Preset", true, GetDesc("auto_preset"));
            #endregion

            LocalizationService.SelectedLocalizeFolder ??= Path.Combine(Launcher.LocalizationDir + LangFileName.Value + "\\");

            #region HOTKEYS
            ToggleCusorHotkey = CFG.Bind(HotkeysUISect, "Toggle Cursor", KeyCode.F1);
            ToggleUIHotkey = CFG.Bind(HotkeysUISect, "Toggle FGT UI", KeyCode.F2);
            DebugUIHotkey = CFG.Bind(HotkeysUISect, "Debug UI", KeyCode.T);

            ToggleFreeCamHotkey = CFG.Bind(HotkeysFCSect, "Toggle Free Camera", KeyCode.F10);
            PauseFreeCamHotkey = CFG.Bind(HotkeysFCSect, "Toggle Free Camera Pause", KeyCode.F11);
            FreeCamMoveUpHotkey = CFG.Bind(HotkeysFCSect, "Move UP", KeyCode.Space);
            FreeCamMoveDownHotkey = CFG.Bind(HotkeysFCSect, "Move Down", KeyCode.LeftControl);
            FreeCamToggleUI = CFG.Bind(HotkeysFCSect, "Hide Free Camera GUI", KeyCode.V);

            EnterFFM = CFG.Bind(HotkeysFFMSect, "Toggle Free Fly Mode", KeyCode.F5);
            MoveUP = CFG.Bind(HotkeysFFMSect, "Move UP", KeyCode.Space);
            MoveDOWN = CFG.Bind(HotkeysFFMSect, "Move DOWN", KeyCode.LeftControl);
            MoveFORWARD = CFG.Bind(HotkeysFFMSect, "Move FORWARD", KeyCode.W);
            MoveBACKWARD = CFG.Bind(HotkeysFFMSect, "Move BACKWARD", KeyCode.S);
            MoveLEFT = CFG.Bind(HotkeysFFMSect, "Move LEFT", KeyCode.A);
            MoveRIGHT = CFG.Bind(HotkeysFFMSect, "Move RIGHT", KeyCode.D);

            CheckpointHotkey = CFG.Bind(HotkeysGPSect, "Checkpoint", KeyCode.C);
            RespawnHotkey = CFG.Bind(HotkeysGPSect, "Respawn", KeyCode.R);
            RestartRunHotkey = CFG.Bind(HotkeysGPSect, "Reset Speedrun", KeyCode.F);
            ResetCheckpointHotkey = CFG.Bind(HotkeysGPSect, "Reset Checkpoint Position", KeyCode.G);
            SkipIntroHotkey = CFG.Bind(HotkeysGPSect, "Skip Intro", KeyCode.Q);
            #endregion

            #region ALL COSMETICS
            AllCosmetics = CFG.Bind(AllCosmeticsSect, "Add All Cosmetics", false, GetDesc("use_all_cosmetics"));
            AllCosmetics.SettingChanged += (sender, args) => {
               if (AllCosmetics.Value)
                    FGTServiceManager.GetService<CosmeticsService>().GrantAllCosmetics();
               else
                    FGTServiceManager.GetService<CosmeticsService>().RemoveAllCosmetics();

            };

            AllCosmeticsAlert = CFG.Bind(AllCosmeticsSect, "Show Popup", true, GetDesc("cosmetics_alert"));
            #endregion

            #region FREE FLY
            FFMSpeedH = CFG.Bind(FFMSect, "Horizontal Speed", 45f);

            FFMSpeedV = CFG.Bind(FFMSect, "Verctical Speed", 25f);

            #endregion

            #region FREE CAMERA
            FreeCamSpeed = CFG.Bind(FCSect, "Camera Speed", 30f);

            FreeCamSens = CFG.Bind(FCSect, "Camera Sensivity", 3f);

            FreeCamZoomSpeed = CFG.Bind(FCSect, "Camera Zoom Speed", 30f);

            FreeCamAudioEffect = CFG.Bind(FCSect, "Audio Effect", true, GetDesc("fc_time_attack_effect"));
            #endregion

            #region WATERMARK
            WatermarkLevel = CFG.Bind(WatermarkSect, "FGT Watermark", Watermark.VersionAndCredits, GetDesc("watermark_level"));
            #endregion

            #region GAMEPLAY
            QualLevel = CFG.Bind(GPSect, "Qualification Type", QualType.LoadRandomRoundAfter);

            ElimLevel = CFG.Bind(GPSect, "Elimination Type", ElimType.LoadRandomRoundAfter);

            WinLevel = CFG.Bind(GPSect, "Win Type", WinType.LoadRandomRoundAfter);

            GravZoneEffect = CFG.Bind(GPSect, "Gravity Zone Effect", false, GetDesc("grav_zone_effect"));
            GravZoneEffect.SettingChanged += (sender, args) => {
                ConfigAction();
            };

            InvisibleCheckpoint = CFG.Bind(GPSect, "Invisible Checkpoint", false, GetDesc("hide_checkpoint"));
            InvisibleCheckpoint.SettingChanged += (sender, args) => {
                if (StateManager.IsInGameplay)
                  FGBehaviour.CurrentGPState.Spawnpoint.GetComponent<MeshRenderer>().enabled = !InvisibleCheckpoint.Value;
            };
            RealHardMode = CFG.Bind(GPSect, "Hard Mode", false, GetDesc("hard_mode"));

            RCDelay = CFG.Bind(GPSect, "Delay", 0.5f, GetDesc("cr_delay"));

            SkipIntroTime = CFG.Bind(GPSect, "Skip Intro Time", 0.5f, GetDesc("skip_intro_time"));

            RoundsFilter = CFG.Bind(GPSect, "Random Rounds Filter", RandomRoundsFilter.All, GetDesc("round_filter"));

            ColliderView = CFG.Bind(GPSect, "Collider View", false, GetDesc("collider_view"));
            ColliderView.SettingChanged += (sender, args) => {
                ConfigAction();
            };

            RandomizeRings = CFG.Bind(GPSect, "Random Roll Levels Rings", false, GetDesc("random_rings"));
            RandomizeRings.SettingChanged += (sender, args) => {
                ConfigAction();
            };

            CameraDistance = CFG.Bind(GPSect, "Camera Distance", 0f, GetDesc("custom_camera_distance"));
            CameraDistance.SettingChanged += (sender, args) => {
                if (StateManager.IsInGameplay && CameraDistance.Value > 0)
                {
                    foreach (FallGuysCameraAvoidence camFov in Resources.FindObjectsOfTypeAll<FallGuysCameraAvoidence>())
                        camFov._maxDistance = CameraDistance.Value;
                }
            };

            RandomMusic = CFG.Bind(GPSect, "Random Music", false, GetDesc("random_music"));
            RandomMusic.SettingChanged += (sender, args) => {
                ConfigAction();
            };
            RandomMusic.SettingChanged += (sender, args) => {
                ConfigAction();
            };

            FastLoad = CFG.Bind(GPSect, "Fast Load", false, GetDesc("fast_load"));
            #endregion

            #region PHYSICS
            DiveSens = CFG.Bind(CPSect, "Dive Sensivity", 70f, GetDesc("dive_sens"));
            DiveSens.SettingChanged += (sender, args) => {
                ConfigAction();
            };

            OldPhysics = CFG.Bind(CPSect, "Old Physics", false, GetDesc("old_char_phys"));
            DiveSens.SettingChanged += (sender, args) => {
                ConfigAction();
            };
            #endregion

            #region SPEEDRUNNING
            SpeedrunMode = CFG.Bind(SPSect, "Enable Speedrun Mode", false, GetDesc("speedrun_mode"));
            SpeedrunMode.SettingChanged += (sender, args) => {
                if (!SpeedrunMode.Value && FGTServiceManager.GetService<SpeedrunService>().SpeedrunState > SpeedrunService.RunState.Inactive)
                    FGTServiceManager.GetService<SpeedrunService>().HandleState(SpeedrunService.RunState.Inactive);
                ConfigAction();
            };
            SPRespawnCD = CFG.Bind(SPSect, "Restart Run Cooldown", 0.75f, GetDesc("speedrun_restart_cd"));

            SpeedrunUI = CFG.Bind(SPSect, "Enable Run-stats UI", true, GetDesc("speedrun_stats_ui"));

            SPResetPoints = CFG.Bind(SPSect, "Reset Points After Restart", true, GetDesc("speedrun_points_drop"));

            OldSPContinue = CFG.Bind(SPSect, "Old Speedrun Mode", false, GetDesc("speedrun_old_mode"));

            SPInstaStart = CFG.Bind(SPSect, "Instantly Start", false, GetDesc("speedrun_insta_start"));

            RespawnAtCheckpoint = CFG.Bind(SPSect, "Respawn At Checkpoint", false, GetDesc("speedrun_checkpoint_res"));
            #endregion

            #region POWERUPS
            Powerup = CFG.Bind(PWSect, "Selected Powerup", SelectedPowerup.RollingBall, GetDesc("selected_powerup"));
            Powerup.SettingChanged += (sender, args) => {
                if (StateManager.IsInGameplay)
                    FGBehaviour.SetPowerup(Powerup.Value);
            };

            PowerupInventory = CFG.Bind(PWSect, "Inventory UI", true, GetDesc("powerup_ui"));
            PowerupInventory.SettingChanged += (sender, args) => {
                ConfigAction();
            };

            InfPowerups = CFG.Bind(PWSect, "Infinite Usages", true, GetDesc("inf_powerup"));
            InfPowerups.SettingChanged += (sender, args) => {
                ConfigAction();
            };

            PowerupLength = CFG.Bind(PWSect, "Durination", float.MaxValue, GetDesc("powerup_length"));
            PowerupLength.SettingChanged += (sender, args) => {
                ConfigAction();
            };

            PowerupAmount = CFG.Bind(PWSect, "Amount", (byte)255, GetDesc("powerup_amount"));
            PowerupAmount.SettingChanged += (sender, args) => {
                ConfigAction();
            };
            #endregion

            #region FGC
            AllowFGCAutosaves = CFG.Bind(FGCSect, "Enable Autosaves", true, GetDesc("fgc_autosaves"));
            TargetFGCSaveTime = CFG.Bind(FGCSect, "Autosave Time", 300f, GetDesc("fgc_time_between_autosaves"));
            TargetFGCSaveTime.SettingChanged += (sender, args) => {
                if (TargetFGCSaveTime.Value < 20)
                    TargetFGCSaveTime.Value = 20;
            };
            ShowAutosaveTimer = CFG.Bind(FGCSect, "Show Autosave Timer", true, GetDesc("fgc_show_autosave_timer"));
            EnableLocalAutosaves = CFG.Bind(FGCSect, "Enable Local Autosaves", true, GetDesc("fgc_local_saves"));
            //PauseTimerExplore = CFG.Bind(FGCSect, "Pause Autosave Timer While Testing Level", false, GetDesc("fgc_local_saves"));
            #endregion

            #region MIRRORS
            ContentMirror = CFG.Bind(FGCSect, "Download Source", MirrorType.Auto, GetDesc("download_mirror"));
            ContentSourceOverride = CFG.Bind(FGCSect, "Download Source Override", string.Empty, GetDesc("download_mirror_override"));
            #endregion
        }
    }
}
