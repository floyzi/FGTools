extern alias wle;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using FGTools.HarmonyPatches;
using FGTools.Internal.Behaviours;
using FGTools.Internal.Behaviours.ServerSide;
using FGTools.Internal.Extensions;
using FGTools.LocalServer;
using HarmonyLib;
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
    [BepInPlugin(GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    [BepInIncompatibility("flz.fg.desktop.cheats")]
    public class Launcher : BasePlugin
    {
        public readonly struct BuildDetails
        {
            public readonly string UI_Version;
            public readonly string Config;
            public readonly string Commit;
            public readonly Guid GUID;
            public readonly DateTime BuildDate;
            public readonly string[] Defines;
            public readonly string Version;

            public BuildDetails(string config, string ui_version, string file_version, string commit, string date, string guid, string defines)
            {
                Config = config;
                Commit = commit;
                UI_Version = ui_version;
                Version = file_version;

                if (long.TryParse(date, out long unixTimestamp))
                    BuildDate = DateTimeOffset.FromUnixTimeSeconds(unixTimestamp).UtcDateTime;

                GUID = Guid.Parse(guid);

#if DEV
                var except = new HashSet<string>() { "TRACE", "NET", "NET6_0", "NETCOREAPP", "DEV" };
                Defines = [.. defines.Split(';', StringSplitOptions.RemoveEmptyEntries).Where(d => !except.Contains(d))];
#endif
            }

            public override string ToString()
            {
                return $"Env: {Config} Commit: #{GetCommit()} Build Date: {BuildDate}";
            }

            internal string GetCommit()
            {
                return Commit.Length > 12 ? Commit[..12] : Commit;
            }

            internal string GetDefines()
            {
#if DEV
                if (Defines == null)
                    return "?";

                return string.Join(", ", Defines);
#else
                return "?";
#endif
            }
        }

        internal readonly static Harmony GlobalHarmony = new(HarmonyGUID);
        internal readonly static Harmony OfflineHarmony = new(OfflineHarmonyGUID);
        internal readonly static Harmony FGCHarmony = new(FraggleHarmonyGUID);
        internal readonly static Harmony PermanentHarmony = new(PermanentHarmonyGUID);
        internal readonly static Harmony ThemesHarmony = new(ThemesHarmonyGUID);
        internal readonly static Harmony ServerHarmony = new(ServerHarmonyGUID);

        internal static BuildDetails BuildInfo;
        internal static DateTime StartupTime = DateTime.UtcNow;

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
                    _ => DefaultName,
                };
            } 
        }

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
        #endregion

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern int MessageBox(IntPtr ptr, string msg, string title, uint type);

        public override void Load()
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();

                var version = FileVersionInfo.GetVersionInfo(assembly.Location);
                var commit = version.ProductVersion.Split('+');
                var metadata = assembly.GetCustomAttributes<AssemblyMetadataAttribute>().ToList();
                var cfg = assembly.GetCustomAttributes<AssemblyConfigurationAttribute>().ToList()[0].Configuration;

                var buildDate = metadata.FirstOrDefault(x => x.Key == "BuildDate")?.Value;
                var buildGuid = metadata.FirstOrDefault(x => x.Key == "BuildGuid")?.Value;
                string defines = null;
#if DEV
                defines = metadata.FirstOrDefault(x => x.Key == "DefineConstants")?.Value;
#endif
                BuildInfo = new BuildDetails(cfg, MyPluginInfo.PLUGIN_VERSION, version.FileVersion, commit.Length > 1 ? commit[1] : "LOCAL BUILD", buildDate, buildGuid, defines);

                FLZ_Extensions.TryRegisterTypeInIl2cpp<FGTBehaviour>();
                FLZ_Extensions.TryRegisterTypeInIl2cpp<ToolsBehaviour>();

                var monoMain = new GameObject() { name = DefaultName };
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

                Log.LogMessage($" --- ");
                Log.LogMessage($"{DisplayName} V{BuildInfo.UI_Version}");
                Log.LogMessage($"{Description}");
                Log.LogMessage($"{BuildInfo.ToString()}");
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

                    if (!AppDomain.CurrentDomain.GetAssemblies().Any(a => string.Equals(a.GetName().Name, Definitions.UniverseLib, StringComparison.OrdinalIgnoreCase)))
                        Log.LogWarning($"Unable to find UniverseLib as loaded dll ({Definitions.UniverseLib}.dll). Please install UniverseLib to ensure correct work of {DisplayName}, it comes up with every {DisplayName} release. UniverseLib needed to render {DisplayName} UI");

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
                $"FATAL ERROR - {DisplayName} V{BuildInfo.UI_Version} (#{BuildInfo.GetCommit()})",
                $"Unable to launch {DisplayName} because important files are missing. If you can't fix this by yourself ask for help in the discord server ({DiscordUrl}) or reinstall {DisplayName}\n\nWhat content are missing...\n\n {string.Join($"\n\n", MissingData)}");
        }

        void OnCrash(Exception e)
        {
            Log.LogFatal($"[Launcher] Startup failed. Exception! {e}");

            FLZ_Extensions.QuitWithMessage(
                $"FATAL ERROR - {DisplayName} V{BuildInfo.UI_Version} (#{BuildInfo.GetCommit()})",
                $"{DisplayName} encountered an exception on startup. This is NOT supposed to happen!\nIf you can't fix this by yourself try reinstalling {DisplayName}. If reinstalling doesn't help ask for help in the discord server ({DiscordUrl})\nNOTE: If this happens after the Fall Guys update this means that Mediatonic changed some of the stuff that affects {DisplayName} work, wait for an update that will fix this.\n\nSome nerd info\nException: {e.Message}\nStackTrace: {e.StackTrace}\n\nGame will be closed");
        }

        readonly List<string> MissingData = [];
        bool Valid()
        {
            Log.LogInfo("[Launcher] Validating...");

            string[] dirs = [CommonDir, AssetsDir, LocalizationDir, LibDir];
            string[] files = [BundlePath, StaticConfigDescs, Path.Combine(LibDir, "discord_game_sdk.dll"), Path.Combine(LibDir, "discord_api.dll"), Path.Combine(LibDir, "NAudio.Core.dll")];

            foreach (var folder in dirs)
            {
                if (!Directory.Exists(folder))
                    MissingData.Add(folder);
            }

            foreach (var file in files)
            {
                if (!File.Exists(file))
                    MissingData.Add(file);
            }

            return MissingData.Count == 0;
        }

        void StartUp()
        {
            Log.LogMessage("[Launcher] All OK... Startup");
            FGTBehaviour._mainBehaviour.StartUp();
        }
    }
}
