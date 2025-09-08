using FGClient;
using FGTools.Internal.Behaviours;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FGTools.HarmonyPatches
{
    public class ThemePatches
    {
        [HarmonyPatch(typeof(MainMenuManager), nameof(MainMenuManager.PlayMenuMusic))]
        [HarmonyPrefix]
        static bool PlayMenuMusic(MainMenuManager __instance, int playbackPosition = -1)
        {
            var provider = __instance.GetComponent<MenuAudioProvider>() ?? __instance.gameObject.AddComponent<MenuAudioProvider>();
            provider?.PlayMusic(true);
            return false;
        }

        [HarmonyPatch(typeof(MainMenuManager), nameof(MainMenuManager.PauseMusic))]
        [HarmonyPrefix]
        static bool PauseMusic(MainMenuManager __instance, bool immediate = false)
        {
            return false;
        }

        [HarmonyPatch(typeof(MainMenuManager), nameof(MainMenuManager.StopMusic))]
        [HarmonyPrefix]
        static bool StopMusic(MainMenuManager __instance, bool immediate = false)
        {
            var provider = __instance.GetComponent<MenuAudioProvider>();
            provider?.StopMusic();
            return false;
        }
    }
}
