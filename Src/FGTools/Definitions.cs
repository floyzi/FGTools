global using static Definitions;
using UnityEngine;
static class Definitions
{
    #region URLS
    internal const string URLBase = "https://floyzi.github.io/FGTools/";
    internal const string NewsfeedImgsURL = $"{URLBase}images/newsfeed/";
    internal const string DiscordUrl = "https://discord.gg/PEysxvSE3x";
    internal const string FG_ExploreAPI = "https://level-gateway.fallguys.oncatapult.com/api/v1/round_pools";
    internal const string FGAnalyst_ExploreAPI = "https://cloudseeker.xyz/api/cv2/creative-explore/?only_list=1";
    #endregion

    #region META
#if DEV
    internal const string DefaultName = "SlopTools";
#else
    internal const string DefaultName = "FGTools";
#endif
    internal const string BirthdayName = "It's My Birthday Tools";
    internal const string FoolsName = "FGStool";
    internal const string Description = "Fall Guys level loader by @floyzi102 on Twitter";
#if PROD
    internal const string DownloadSource = "production";
#else
    internal const string DownloadSource = "closed_beta";
#endif
    internal readonly static string[] TargetFGVersions = ["20.0.2"];
    internal const long DiscordAppID = 1138469244430979143;
    internal const string CurrentFGBackground = "Generic_UI_SeasonS11Background_Canvas_Variant";
#endregion

    #region COLORS
    internal const string FGT_Error_Color = "#d13434";
    internal const string FGT_Info_Color = "#57bbeb";
    internal const string FGT_Warning_Color = "#ffaa42";
    internal readonly static Color BuildInfoColor = new(0.3764f, 0.0156f, 0.0156f, 1f);
    internal readonly static Color GUIRed = new(0.3f, 0.2f, 0.2f);
    #endregion

    #region GUIDS
    internal const string GUID = "flz.fgt";
    internal const string UniverseGUID = $"{GUID}.universe.ui";
    internal const string HarmonyGUID = $"{GUID}.harmony";
    internal const string OfflineHarmonyGUID = $"{GUID}.harmony.offline";
    internal const string FraggleHarmonyGUID = $"{GUID}.harmony.fraggle";
    internal const string ThemesHarmonyGUID = $"{GUID}.harmony.themes";
    internal const string ServerHarmonyGUID = $"{GUID}.harmony.server";
    internal const string PermanentHarmonyGUID = $"{GUID}.harmony.permanent";
    #endregion

    #region ASSEMBLIES
#if !DEV
    internal const string UniverseLib = $"UniverseLib.IL2CPP.Interop";
#else
    internal const string UniverseLib = $"UniverseLib.BIE.IL2CPP.Interop";
#endif
    #endregion
}