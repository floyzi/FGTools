using BepInEx.Logging;
using FG.Common.CMS;
using FGTools.Services.Logic;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using static FGTools.Internal.Extensions.FLZ_Extensions;
namespace FGTools.Services
{
    internal class LocalizationService : FGTService
    {
        public static List<string> KnownLocales = [];
        public static Dictionary<string, string> Fallback = [];
        public static Dictionary<string, string> LocalizedStrings = [];
        public static string SelectedLocalizeFolder = null;
        public override void RegisterService()
        {
            if (Config.Config.LangFileName.Value == null || Config.Config.LangFileName.Value == string.Empty)
                Config.Config.LangFileName.Value = "en";

            string targetPatn = null;
            string targetDir = Path.Combine(Launcher.LocalizationDir, "en", "locale.json");
            string backupDir = Path.Combine(Launcher.AssetsDir, "locale.json");

            if (File.Exists(targetDir))
                targetPatn = targetDir;
            else if (File.Exists(backupDir))
                targetPatn = backupDir;
            else
                FGTLog(LogLevel.Warning, base.GetType(), "Can't find any localization file to use!!");

            Fallback = ParseLocalization(targetPatn);

            foreach (var dir in Directory.GetDirectories(Launcher.LocalizationDir))
                KnownLocales.Add(Path.GetFileName(dir).ToUpper());

            FGTLog(LogLevel.Info, base.GetType(), $"Setup complete");
        }

        Dictionary<string, string> ParseLocalization(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                FGTLog(LogLevel.Error, base.GetType(), $"Invalid path \"{path}\" provided to {nameof(ParseLocalization)} !");
                return [];
            }    
            var locale = JsonSerializer.Deserialize<List<Dictionary<string, string>>>(File.ReadAllText(path));

            if (locale == null || locale.Count == 0)
                return [];

            var res = new Dictionary<string, string>();

            const string Key = "key";
            const string Value = "value";


            foreach (var itm in locale)
            {
                if (itm.TryGetValue(Key, out var key) && itm.TryGetValue(Value, out var value))
                {
                    res[key] = value.Replace("{loaderName}", Launcher.DisplayName).Replace("{discordUrl}", DiscordUrl).Replace("\"\"", "\"").Trim('\"');
                }
            }


            return res;
        }

        public void SetupLocalization(string localization)
        {
            LocalizedStrings.Clear();

            FGTLog(LogLevel.Info, base.GetType(), $"Setup for {Path.GetFileName(Path.GetDirectoryName(localization))}");

            LocalizedStrings = ParseLocalization(localization);
        }

        public static string LocalizedStr(string key, object[] format = null, bool fromCMS = false)
        {
            string result;

            if (LocalizedStrings.ContainsKey(key))
                result = LocalizedStrings[key];
            else if (Fallback.ContainsKey(key))
                result = Fallback[key];
            else
                return $"<b><color=red>MISSING:</color></b> {key}";

            if (fromCMS && CMSLoader.Instance != null && result != null)
            {
                if (!CMSLoader.Instance._localisedStrings._localisedStrings.ContainsKey(key))
                    CMSLoader.Instance._localisedStrings._localisedStrings.Add(key, result);
                return key;
            }

            if (format != null)
            {
                int waitingForFormat = Regex.Matches(result, @"\{\d+\}").Count;
                if (format.Length != waitingForFormat)
                {
                    FGTLog(LogLevel.Warning, typeof(LocalizationService), $"Tried to format string \"{key}\" with incorrect number of entries. Expected {waitingForFormat} but got {format.Length}");
                    return result;
                }

                result = string.Format(result, format);
            }

            return result;
        }

        public override void UpdateService()
        {
        }

        public override void DrawGUI()
        {
        }

        public override void OnAppFocus(bool focus)
        {

        }

        public override void OnAppQuit()
        {

        }
    }
}
