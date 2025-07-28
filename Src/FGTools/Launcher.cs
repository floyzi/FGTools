extern alias wle;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using FGTools.HarmonyPatches;
using FGTools.Internal.Behaviours;
using FGTools.LocalServer;
using FGTools.Services;
using FGTools.States.Logic;
using HarmonyLib;
using Il2CppInterop.Runtime.Injection;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEngine;
using UniverseLib.UI;
using static FGTools.Config.ConfigManager;
using static FGTools.HarmonyPatches.SnowyScrapPatch;

namespace FGTools
{
    [BepInPlugin(GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    public class Plugin : BasePlugin
    {
        public readonly struct BuildDetails
        {
            public readonly string Version;
            public readonly string Config;
            public readonly string Commit;
            public readonly Guid GUID;
            public readonly DateTime BuildDate;
            public readonly string[] Defines;

            public BuildDetails(string config, string version, string commit, string date, string guid, string defines)
            {
                Config = config;
                Commit = commit;
                Version = version;

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
        public static string LoadingSplash => Path.Combine(AssetsDir, "FGToolsSplash.png");
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
        public static string IntroMusic => Path.Combine(AssetsDir, "gamingIntroOst.wav");
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
        #endregion

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern int MessageBox(IntPtr ptr, string msg, string title, uint type);

        public static void DoHarmonyPatch()
        {
            if (HarmonyPatched)
                return;

            GlobalHarmony.PatchAll(typeof(LogicHarmony));
            GlobalHarmony.PatchAll(typeof(GlobalGameplayPatch));
            GlobalHarmony.PatchAll(typeof(MotorPatches));
            GlobalHarmony.PatchAll(typeof(BlastBallFix));
            GlobalHarmony.PatchAll(typeof(GlobalScoreGamesPatch));
            GlobalHarmony.PatchAll(typeof(TQOSTPatch));
            GlobalHarmony.PatchAll(typeof(TCTPatch));
            GlobalHarmony.PatchAll(typeof(ScoringPatch));
            GlobalHarmony.PatchAll(typeof(PrefabSpawnerPatch));
            GlobalHarmony.PatchAll(typeof(AIPatch));
            GlobalHarmony.PatchAll(typeof(TriggerVolumePatch));
            GlobalHarmony.PatchAll(typeof(PixelPerfectPatch));
            GlobalHarmony.PatchAll(typeof(SnowyScrapPatch));
            GlobalHarmony.PatchAll(typeof(JumpShowdownPlatformsPatch));
            GlobalHarmony.PatchAll(typeof(AttackOfTheTime));
            GlobalHarmony.PatchAll(typeof(FranticExplorer));
            GlobalHarmony.PatchAll(typeof(SelfRespawerFix));

            HarmonyPatched = true;

        }

        public override void Load()
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();

                var commit = FileVersionInfo.GetVersionInfo(assembly.Location).ProductVersion.Split('+');
                var metadata = assembly.GetCustomAttributes<AssemblyMetadataAttribute>().ToList();
                var cfg = assembly.GetCustomAttributes<AssemblyConfigurationAttribute>().ToList()[0].Configuration;

                var buildDate = metadata.FirstOrDefault(x => x.Key == "BuildDate")?.Value;
                var buildGuid = metadata.FirstOrDefault(x => x.Key == "BuildGuid")?.Value;
                string defines = null;
#if DEV
                defines = metadata.FirstOrDefault(x => x.Key == "DefineConstants")?.Value;
#endif
                BuildInfo = new BuildDetails(cfg, MyPluginInfo.PLUGIN_VERSION, commit.Length > 1 ? commit[1] : "LOCAL BUILD", buildDate, buildGuid, defines);

                ClassInjector.RegisterTypeInIl2Cpp<FGTBehaviour>();
                ClassInjector.RegisterTypeInIl2Cpp<ToolsBehaviour>();

                GameObject MonoMain = new() { name = DefaultName };
                MonoMain.AddComponent<FGTBehaviour>();
                MonoMain.hideFlags = HideFlags.HideAndDontSave;

                LoadCFG(Config);

                ClassInjector.RegisterTypeInIl2Cpp<FallGuyBehaviour>();
                ClassInjector.RegisterTypeInIl2Cpp<RespawnTileController>();
                ClassInjector.RegisterTypeInIl2Cpp<SpawnedObjectController>();
                ClassInjector.RegisterTypeInIl2Cpp<LocalPLatformShake>();
                ClassInjector.RegisterTypeInIl2Cpp<FGTController>();
                ClassInjector.RegisterTypeInIl2Cpp<MenuAudioProvider>();
                ClassInjector.RegisterTypeInIl2Cpp<FreeCameraController>();
                ClassInjector.RegisterTypeInIl2Cpp<FFAButtonManager>();
                ClassInjector.RegisterTypeInIl2Cpp<KillZone>();
                ClassInjector.RegisterTypeInIl2Cpp<UGCBubble>();
                ClassInjector.RegisterTypeInIl2Cpp<SelfDestcructObject>();
                ClassInjector.RegisterTypeInIl2Cpp<ServerBehaviour>();
                ClassInjector.RegisterTypeInIl2Cpp<ServerControlledObject>();

                Log.LogMessage($" --- ");
                Log.LogMessage($"{DisplayName} V{BuildInfo.Version}");
                Log.LogMessage($"{Description}");
                Log.LogMessage($"{BuildInfo.ToString()}");
                Log.LogMessage($" --- ");

                PermanentHarmony.PatchAll(typeof(ServerCorePatches));
                //PermanentHarmony.PatchAll(typeof(FGCGameplay));
                PermanentHarmony.PatchAll(typeof(GlobalPatches));
                PermanentHarmony.PatchAll(typeof(CosmeticsService.LockerHarmony));

                if (Valid())
                {
                    if (!Directory.Exists(ThemesDir))
                        Directory.CreateDirectory(ThemesDir);

                    if (!Directory.Exists(BugReportsDir))
                        Directory.CreateDirectory(BugReportsDir);

                    if (!File.Exists(VariantData))
                        File.Create(VariantData);

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
            _ = MessageBox(IntPtr.Zero, $"Unable to launch {DisplayName} because important files are missing. If you can't fix this by yourself ask for help in the discord server ({DiscordUrl}) or reinstall {DisplayName}\n\nWhat content are missing...\n\n {string.Join($"\n\n", MissingData)}", $"FATAL ERROR - {DisplayName} V{BuildInfo.Version} (#{BuildInfo.GetCommit()})", 0);
            Application.Quit();
        }

        void OnCrash(Exception e)
        {
            Log.LogFatal($"[Launcher] Startup failed. Exception! {e}");
            _ = MessageBox(IntPtr.Zero, $"{DisplayName} encountered an exception on startup. This is NOT supposed to happen!\nIf you can't fix this by yourself try reinstalling {DisplayName}. If reinstalling doesn't help ask for help in the discord server ({DiscordUrl})\nNOTE: If this happens after the Fall Guys update this means that Mediatonic changed some of the stuff that affects {DisplayName} work, wait for an update that will fix this.\n\nSome nerd info\nException: {e.Message}\nStackTrace: {e.StackTrace}\n\nGame will be closed", $"FATAL ERROR - {DisplayName} V{BuildInfo.Version} (#{BuildInfo.GetCommit()})", 0);
            Application.Quit();
        }

        readonly List<string> MissingData = [];
        bool Valid()
        {
            Log.LogInfo("[Launcher] Validating...");

            string[] importantDirs = [CommonDir, AssetsDir, LocalizationDir, Path.Combine(AssetsDir, "Lib")];
            string[] importantFiles = [Plugin.BundlePath, StaticConfigDescs];

            foreach (string folder in importantDirs)
            {
                if (!Directory.Exists(folder))
                    MissingData.Add(folder);
            }

            foreach (string file in importantFiles)
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
