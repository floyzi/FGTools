using FGTools.Content.ContentImpl;
using FGTools.Internal;

namespace FGTools.Content
{
    public static class FGTTargetSettings
    {
        public static bool RoundLoader { get; private set; }
        public static bool ShowLoader { get; private set; }
        public static bool CosmeticPresets { get; private set; }
        public static bool Img2Fgc { get; private set; }
        public static bool MediaLoaderImages { get; private set; }
        public static bool MediaLoaderVideos { get; private set; }
        public static bool ThemeBrowserLocal { get; private set; }
        public static bool ThemeBrowserOnline { get; private set; }
        public static bool Statistics { get; private set; }
        public static bool LangSwitcher { get; private set; }
        public static bool GamingIntro { get; private set; }
        public static bool WelcomePopup { get; private set; }
        public static bool CustomThemes { get; private set; }
        public static bool UpdateNotification { get; private set; }
        public static bool FGCLocalSaves { get; private set; }
        public static bool FGCAutosaves { get; private set; }
        public static bool LocalMultiplayer { get; private set; }
        public static bool FGTNewsfeed { get; private set; }
        public static bool RoundRules { get; private set; }
        public static bool VersionWarning { get; private set; }

        public static void Load(FGTTargetSettingsData data)
        {
            RoundLoader = data.GetBool("round_loader", false);
            ShowLoader = data.GetBool("show_loader", false);
            CosmeticPresets = data.GetBool("presets", false);
            Img2Fgc = data.GetBool("img2fgc", false);
            MediaLoaderImages = data.GetBool("media_loader_images", false);
            MediaLoaderVideos = data.GetBool("media_loader_videos", false);
            ThemeBrowserLocal = data.GetBool("misc_theme_browser_local", false);
            ThemeBrowserOnline = data.GetBool("misc_theme_browser_online", false);
            Statistics = data.GetBool("misc_statistics", false);
            LangSwitcher = data.GetBool("misc_lang_switcher", false);
            GamingIntro = data.GetBool("gaming_intro", false);
            WelcomePopup = data.GetBool("welcome_popup", false);
            CustomThemes = data.GetBool("custom_themes", false);
            UpdateNotification = data.GetBool("update_notification", false);
            FGCLocalSaves = data.GetBool("fgc_local_saves", false);
            FGCAutosaves = data.GetBool("fgc_autosaves", false);
            LocalMultiplayer = data.GetBool("local_multiplayer", false);
            FGTNewsfeed = data.GetBool("fgt_newsfeed", false);
            RoundRules = data.GetBool("round_rules", false);
            VersionWarning = data.GetBool("version_warning", false);

            Commands.OnTargetsParsed?.Invoke();
        }
    }
}
