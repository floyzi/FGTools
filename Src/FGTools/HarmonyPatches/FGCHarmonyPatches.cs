extern alias wle;
using System.Linq;
using FG.Common.CMS;
using FG.Common.Fraggle;
using FGTools.Internal;
using FGTools.Internal.Behaviours;
using FGTools.States.Logic;
using HarmonyLib;
using UnityEngine;
using static FGTools.States.Logic.FGTStateManager;

namespace FGTools.HarmonyPatches
{
    public class FGCHarmonyPatches : FGTBase
    {
        public static string GetLevelIcoRPC()
        {
            if (wle.FG.Common.LevelEditorManagerProxy.CurrentLevel != null)
            {
                var mode = wle.FG.Common.LevelEditorManagerProxy.CurrentLevel._gameMode;
                if (mode.ID == "GAMEMODE_GAUNTLET")
                    return "ui_medal_icon_creative_mode_gauntlet";
                else if (mode.ID == "GAMEMODE_SURVIVAL")
                    return "ui_medal_icon_creative_mode_risingslime";
                else
                    return "ui_medal_icon_creative_mode_gauntlet";
            }
            else
                return null;
        }

        public static string GetLevelTheme()
        {
            if (wle.FG.Common.LevelEditorManagerProxy.CurrentLevel != null)
            {
                var theme = wle.FG.Common.LevelEditorManagerProxy.CurrentLevel.GetTheme();
                var strings = CMSLoader.Instance._localisedStrings._localisedStrings;

                if (theme.ID == "THEME_VANILLA")
                    return strings["wle_theme_1"];
                else if (theme.ID == "THEME_RETRO")
                    return strings["wle_theme_retro"];
                else
                    return "Unknown";
            }
            else
                return null;
        }

        public static string GetLevelName()
        {
            if (wle.FG.Common.LevelEditorManagerProxy.CurrentLevel != null && wle.FG.Common.LevelEditorManagerProxy.CurrentLevel.Name != null)
                return wle.FG.Common.LevelEditorManagerProxy.CurrentLevel.Name;
            else
                return "Unknown";
        }

        public static string GetLevelMode()
        {
            if (wle.FG.Common.LevelEditorManagerProxy.CurrentLevel != null && wle.FG.Common.LevelEditorManagerProxy.CurrentLevel.Name != null)
            {
                var mode = wle.FG.Common.LevelEditorManagerProxy.CurrentLevel._gameMode;
                var strings = CMSLoader.Instance._localisedStrings._localisedStrings;

                if (mode.ID == "GAMEMODE_GAUNTLET")
                    return strings["archetype_race"];
                else if (mode.ID == "GAMEMODE_SURVIVAL")
                    return strings["archetype_survival"];
                else if (mode.ID == "GAMEMODE_POINTS")
                    return strings["archetype_points"];
                else
                    return "Unknown";
            }
            else
                return "Unknown";
        }

        [HarmonyPatch(typeof(FraggleCommonManager), nameof(FraggleCommonManager.SetModeToBuild))]
        [HarmonyPrefix]
        static bool SetModeToBuild(FraggleCommonManager __instance, LevelEditorEnteredBuildModeFromExploreModeEvent evt)
        {
            GameActions.OnFGCPlaymodeExit?.Invoke();
            if (StateManager.FGCurrentState == PlayerState.FreeCam)
              StateManager.HandleFGState(PlayerState.Despawned);
            return true;
        }

        [HarmonyPatch(typeof(FraggleCommonManager), nameof(FraggleCommonManager.SetModeToExplore))]
        [HarmonyPrefix]
        static bool SetModeToExplore(FraggleCommonManager __instance, LevelEditorEnteredExploreModeEvent evt)
        {
            GameActions.OnFGCPlaymodeEnter?.Invoke();
            Resources.FindObjectsOfTypeAll<FallGuysCharacterController>().FirstOrDefault().gameObject.AddComponent<FreeCameraController>();
            return true;
        }
    }
}
