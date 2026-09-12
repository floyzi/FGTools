extern alias wle;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using FGTools.HarmonyPatches;
using FGTools.Internal.Behaviours;
using FGTools.Internal.Behaviours.ServerSide;
using FGTools.Internal.Extensions;
using FGTools.LocalServer.Patches;
using HarmonyLib;
using Il2CppSystem.Net;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEngine;
using UniverseLib.UI;
using static FGTools.Config.Config;

namespace FGTools
{
    [BepInPlugin(FGToolsBuildDetails.BepInExID, FGToolsBuildDetails.Name, FGToolsBuildDetails.Version)]
    [BepInIncompatibility("flz.fg.desktop.cheats")]
    public class Launcher : BasePlugin
    {
        internal readonly static Harmony GlobalHarmony = new(HarmonyGUID);
        internal readonly static Harmony OfflineHarmony = new(OfflineHarmonyGUID);
        internal readonly static Harmony FGCHarmony = new(FraggleHarmonyGUID);
        internal readonly static Harmony PermanentHarmony = new(PermanentHarmonyGUID);
        internal readonly static Harmony ThemesHarmony = new(ThemesHarmonyGUID);
        internal readonly static Harmony ServerHarmony = new(ServerHarmonyGUID);

        internal static DateTime StartupTime = DateTime.UtcNow;
        internal static DateTime BuildDate;

        internal static UIBase UniverseUIBase;
        internal static AssetBundle FGToolsBundle;

        internal static bool HarmonyPatched;
        internal static bool FGCHarmonyPatched;
        internal static bool ThemesHarmonyPatched;

        internal static string DisplayName 
        { 
            get
            {
                int[] date = [DateTime.Now.Month, DateTime.Now.Day];

                return date switch
                {
                    [5, 27] => BirthdayName,
                    [4, 1] => FoolsName,
#if !DEV
                    _ => FGToolsBuildDetails.Name,
#else
                    _ => "SlopTools"
#endif
                };
            } 
        }
#if DEV
        internal static HashSet<string> IgnoreDefines = ["TRACE", "NET", "NET6_0", "NETCOREAPP", FGToolsBuildDetails.Config.ToUpper()];
#endif

        #region PATHS
        public static string CommonDir => Path.Combine(Paths.PluginPath, "FGTools\\");
        public static string OldCommonDir => Path.Combine(Paths.PluginPath, "FallGuysTools\\");
        public static string AssetsDir => Path.Combine(CommonDir, "Assets\\");
        public static string LocalizationDir => Path.Combine(CommonDir, "Localization\\");
        public static string FallbackConfigDescs => Path.Combine(AssetsDir, "config-descs.txt");
        public static string PresetsDir => Path.Combine(CommonDir, "Presets\\");
        public static string CMSRounds => Path.Combine(AssetsDir, "cmsRounds.txt");
        public static string ShowList => Path.Combine(AssetsDir, "showList.txt");
        public static string RunsData => Path.Combine(AssetsDir, "speedrunData.txt");
        public static string FallbackCredits => Path.Combine(AssetsDir, "credits.txt");
        public static string VariantData => Path.Combine(AssetsDir, "variantData.txt");
        public static string LoadingScreen => Path.Combine(AssetsDir, "loading.png");
        public static string VidDir => Path.Combine(CommonDir, "Videos\\");
        public static string ImgDir => Path.Combine(CommonDir, "Images\\");
        public static string CustomBGDir => Path.Combine(ImgDir, "CustomBG\\");
        public static string IMG2FGCExe => Path.Combine(AssetsDir, "ImgToFGC.exe");
        public static string IMG2FGCSource => Path.Combine(AssetsDir, "ImgToFGC.py");
        public static string EventsList => Path.Combine(AssetsDir, "events.txt");
        public static string Changelog => Path.Combine(CommonDir, "changelog.txt");
        public static string BundlesDir => Path.Combine(CommonDir, "Bundles\\");
        public static string BundlesOutput => Path.Combine(BundlesDir, "AllScenes.txt");
        public static string ThemesDir => Path.Combine(CommonDir, "Themes\\");
        public static string IntroBundle => Path.Combine(AssetsDir, "fgtoolsgamingintro");
        public static string BundlePath => Path.Combine(AssetsDir, "fgtools_content");
        public static string IntroMusic => Path.Combine(AssetsDir, "INTRO_MUSIC.wav");
        public static string StatsFile => Path.Combine(AssetsDir, "stats.json");
        public static string IconsDir => Path.Combine(AssetsDir, "Icons\\");
        public static string CustomFavList => Path.Combine(AssetsDir, "favList.json");
        public static string ExploreBackup => Path.Combine(AssetsDir, "explore-codes.json");
        public static string LangCodes => Path.Combine(AssetsDir, "language-codes.csv");
        public static string EventsListNew => Path.Combine(AssetsDir, "events.json");
        public static string BugReportsDir => Path.Combine(CommonDir, "Reports\\");
        public static string RoundOptionsData => Path.Combine(AssetsDir, "roundData.json");
        public static string RunsDataNew => Path.Combine(AssetsDir, "speedrunData.json");
        public static string UGCShows => Path.Combine(CommonDir, "Shows\\");
        public static string ChangelogV2 => Path.Combine(AssetsDir, "changelog.json");
        public static string FGCAutosavesDir => Path.Combine(CommonDir, "FGCAutosaves\\");
        public static string FGCAutosavesMetadata => Path.Combine(FGCAutosavesDir, "metadata");
        public static string JsonTesting => Path.Combine(CommonDir, "preloaded.json");
        public static string StaticConfigDescs => Path.Combine(AssetsDir, "config_descs.json");
        public static string BuildScenesList => Path.Combine(AssetsDir, "build_scenes.json");
        public static string ControllerDatasList => Path.Combine(AssetsDir, "controller_presets.json");
        public static string ExploreBackupV2 => Path.Combine(AssetsDir, "explore-codes_V2.json");
        public static string Splash => Path.Combine(AssetsDir, "splash.png");
        public static string LibDir => Path.Combine(AssetsDir, "Lib");
        public static string Crashpad => Path.Combine(CommonDir, "FGToolsCrashpad.exe");
        #endregion

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern int MessageBox(IntPtr ptr, string msg, string title, uint type);

        readonly List<string> _missingData = [];

        public override void Load()
        {
            try
            {
                FLZ_Extensions.TryRegisterTypeInIl2cpp<FGTBehaviour>();
                FLZ_Extensions.TryRegisterTypeInIl2cpp<ToolsBehaviour>();

                var monoMain = new GameObject(FGToolsBuildDetails.Name);
                monoMain.AddComponent<FGTBehaviour>();
                monoMain.hideFlags = HideFlags.HideAndDontSave;

                LoadCFG(Config);

                FLZ_Extensions.TryRegisterTypeInIl2cpp<FallGuyBehaviour>();
                FLZ_Extensions.TryRegisterTypeInIl2cpp<FGTController>();
                FLZ_Extensions.TryRegisterTypeInIl2cpp<MenuAudioProvider>();
                FLZ_Extensions.TryRegisterTypeInIl2cpp<FreeCameraController>();
                FLZ_Extensions.TryRegisterTypeInIl2cpp<SimpleTrigger>();
                FLZ_Extensions.TryRegisterTypeInIl2cpp<ServerBehaviour>();
                FLZ_Extensions.TryRegisterTypeInIl2cpp<ServerControlledObject>();
                FLZ_Extensions.TryRegisterTypeInIl2cpp<PrefabSpawnerController>();
                FLZ_Extensions.TryRegisterTypeInIl2cpp<CosmeticSearchBar>();

                BuildDate = DateTimeOffset.FromUnixTimeSeconds(FGToolsBuildDetails.BuildTimestamp).UtcDateTime;

                Log.LogMessage($" --- ");
                Log.LogMessage($"{DisplayName} V{FGToolsBuildDetails.Version} [#{FGToolsBuildDetails.BuildNumber}]");
                Log.LogMessage($"{FGToolsBuildDetails.Description}");
                Log.LogMessage($"Env: {FGToolsBuildDetails.Config} | Commit: #{FGToolsBuildDetails.CommitHashShort} | Build Date: {BuildDate}");
                Log.LogMessage($" --- ");

                PermanentHarmony.PatchAll(typeof(ServerCorePatches));
                PermanentHarmony.PatchAll(typeof(GlobalPatches));
                PermanentHarmony.PatchAll(typeof(LockerHarmony));

                if (Valid())
                {
                    if (!Directory.Exists(ThemesDir))
                        Directory.CreateDirectory(ThemesDir);

                    if (!Directory.Exists(BugReportsDir))
                        Directory.CreateDirectory(BugReportsDir);

                    if (!File.Exists(VariantData))
                        File.Create(VariantData);

                    var dlls = Directory.GetFiles(Paths.PluginPath, "*.dll", SearchOption.AllDirectories);
                    if (!dlls.Any(x => x.EndsWith($"{Constants.UniverseLib}.dll")))
                    {
                        FLZ_Extensions.QuitWithMessage(
                            $"FATAL ERROR - {DisplayName} V{FGToolsBuildDetails.Version} (#{FGToolsBuildDetails.CommitHashShort} [{FGToolsBuildDetails.BuildNumber}])",
                            $"Unable to find UniverseLib dll ({Constants.UniverseLib}.dll). Please install UniverseLib to ensure correct work of {DisplayName}, it comes up with every {DisplayName} release. UniverseLib is needed to render {DisplayName} UI");
                        return;
                    }

                    var dll = dlls.FirstOrDefault(x => x.EndsWith($"{Constants.UniverseLib}.dll"));
                    if (!string.IsNullOrEmpty(dll) && File.Exists(dll))
                    {
                        var verInfo = FileVersionInfo.GetVersionInfo(dll);
                        if (verInfo.FileVersion != TargetUniverseVersion)
                        {
                            FLZ_Extensions.QuitWithMessage(
                                $"FATAL ERROR - {DisplayName} V{FGToolsBuildDetails.Version} (#{FGToolsBuildDetails.CommitHashShort} [{FGToolsBuildDetails.BuildNumber}])",
                                $"Installed UniverseLib version \"{verInfo.FileVersion}\" does not match the required version \"{TargetUniverseVersion}\". Please update UniverseLib to the same version that comes with {DisplayName} release. UniverseLib is needed to render {DisplayName} UI");
                            return;
                        }
                    }
                    else
                        Log.LogWarning("[Launcher] Had to skip UniverseLib ver check");

                    StartUp();
                }
                else
                    OnValidateFail();
            }
            catch (Exception e)
            {
                OnCrash(e);
            }
        }

        void OnValidateFail()
        {
            Log.LogFatal("[Launcher] Startup failed. Certain files or folders missing!");

            FLZ_Extensions.QuitWithMessage(
                $"FATAL ERROR - {DisplayName} V{FGToolsBuildDetails.Version} (#{FGToolsBuildDetails.CommitHashShort} [{FGToolsBuildDetails.BuildNumber}])",
                $"Unable to launch {DisplayName} because important files are missing. If you can't fix this by yourself ask for help in the discord server ({DiscordUrl}) or reinstall {DisplayName}\n\nWhat content are missing...\n\n {string.Join($"\n\n", _missingData)}");
        }

        void OnCrash(Exception e)
        {
            Log.LogFatal($"[Launcher] Startup failed. Exception! {e}");

            FLZ_Extensions.QuitWithMessage(
                $"FATAL ERROR - {DisplayName} V{FGToolsBuildDetails.Version} (#{FGToolsBuildDetails.CommitHashShort})",
                $"{DisplayName} encountered an exception on startup. This is NOT supposed to happen!\nIf you can't fix this by yourself try reinstalling {DisplayName}. If reinstalling doesn't help ask for help in the discord server ({DiscordUrl})\nNOTE: If this happens after the Fall Guys update this means that Mediatonic changed some of the stuff that affects {DisplayName} work, wait for an update that will fix this.\n\nSome nerd info\nException: {e.Message}\nStackTrace: {e.StackTrace}\n\nGame will be closed");
        }

        bool Valid()
        {
            Log.LogInfo("[Launcher] Validating...");

            string[] dirs = [CommonDir, AssetsDir, LocalizationDir, LibDir];
            string[] files = [BundlePath, StaticConfigDescs, Path.Combine(LibDir, "discord_game_sdk.dll"), Path.Combine(LibDir, "discord_api.dll"), Path.Combine(LibDir, "NAudio.Core.dll")];

            foreach (var folder in dirs)
            {
                if (!Directory.Exists(folder))
                    _missingData.Add(folder);
            }

            foreach (var file in files)
            {
                if (!File.Exists(file))
                    _missingData.Add(file);
            }

            return _missingData.Count == 0;
        }

        void StartUp()
        {
            Log.LogMessage("[Launcher] All OK... Startup");
            FGTBehaviour._mainBehaviour.StartUp();
        }
    }
}
