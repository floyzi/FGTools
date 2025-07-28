using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using BepInEx.Logging;
using FG.Common.CMS;
using FGTools.Config;
using FGTools.Services.Logic;
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
            if (ConfigManager.LangFileName.Value == null || ConfigManager.LangFileName.Value == string.Empty)
                ConfigManager.LangFileName.Value = "en";

            string targetPatn = null;
            string targetDir = Path.Combine(Plugin.LocalizationDir, "en", "locale.json");
            string backupDir = Path.Combine(Plugin.AssetsDir, "locale.json");

            if (File.Exists(targetDir))
                targetPatn = targetDir;
            else if (File.Exists(backupDir))
                targetPatn = backupDir;
            else
                FGTLog(LogLevel.Warning, base.GetType(), "Can't find any localization file to use!!");

            Fallback = ParseLocalization(targetPatn);

            foreach (var dir in Directory.GetDirectories(Plugin.LocalizationDir))
                KnownLocales.Add(Path.GetFileName(dir).ToUpper());

            FGTLog(LogLevel.Info, base.GetType(), $"Setup complete");
        }

        Dictionary<string, string> ParseLocalization(string path)
        {
            var locale = JsonSerializer.Deserialize<List<Dictionary<string, string>>>(File.ReadAllText(path));

            if (locale == null || locale.Count == 0)
                return new();

            var res = new Dictionary<string, string>();

            const string Key = "key";
            const string Value = "value";


            foreach (var itm in locale)
            {
                if (itm.TryGetValue(Key, out var key) && itm.TryGetValue(Value, out var value))
                {
                    res[key] = InitString(value);
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

        static string InitString(string value) => value.Replace("{loaderName}", Plugin.DisplayName).Replace("{discordUrl}", DiscordUrl).Replace("\"\"", "\"").Trim('\"');

        public static string LocalizedStr(string key, object[] format = null, bool fromCMS = false)
        {
            string result = null;

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
    }
}
