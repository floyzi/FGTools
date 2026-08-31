global using static Constants;
using FGTools;
using UnityEngine;
static class Constants
{
    #region URLS
    internal const string DiscordUrl = "https://discord.gg/PEysxvSE3x";
    internal const string FG_ExploreAPI = "https://level-gateway.fallguys.oncatapult.com/api/v1/round_pools";
    internal const string FGAnalyst_ExploreAPI = "https://cloudseeker.xyz/api/cv2/creative-explore/?only_list=1";
    #endregion

    #region META
    internal const string BirthdayName = "It's My Birthday Tools";
    internal const string FoolsName = "FGStool";
#if PROD
    internal const string DownloadSource = "production";
#else
    internal const string DownloadSource = "closed_beta";
#endif
    internal readonly static string[] TargetFGVersions = ["21.3.1"];
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
    internal const string UniverseGUID = $"{FGToolsBuildDetails.BepInExID}.universe.ui";
    internal const string HarmonyGUID = $"{FGToolsBuildDetails.BepInExID}.harmony";
    internal const string OfflineHarmonyGUID = $"{FGToolsBuildDetails.BepInExID}.harmony.offline";
    internal const string FraggleHarmonyGUID = $"{FGToolsBuildDetails.BepInExID}.harmony.fraggle";
    internal const string ThemesHarmonyGUID = $"{FGToolsBuildDetails.BepInExID}.harmony.themes";
    internal const string ServerHarmonyGUID = $"{FGToolsBuildDetails.BepInExID}.harmony.server";
    internal const string PermanentHarmonyGUID = $"{FGToolsBuildDetails.BepInExID}.harmony.permanent";
    #endregion

    #region ASSEMBLIES
    internal const string UniverseLib = $"UniverseLib.BIE.IL2CPP.Interop";
    #endregion
}