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
        [HarmonyPatch(typeof(MainMenuManager), "PlayMenuMusic")]
        [HarmonyPrefix]
        static bool PlayMenuMusic(MainMenuManager __instance, int playbackPosition)
        {
            MenuAudioProvider mainMenuCustomAudio = __instance.GetComponent<MenuAudioProvider>() ?? __instance.gameObject.AddComponent<MenuAudioProvider>();
            mainMenuCustomAudio?.PlayMusic(true);
            return false;
        }

        [HarmonyPatch(typeof(MainMenuManager), "PauseMusic")]
        [HarmonyPrefix]
        static bool PauseMusic(MainMenuManager __instance, bool immediate)
        {
            return false;
        }

        [HarmonyPatch(typeof(MainMenuManager), "StopMusic")]
        [HarmonyPrefix]
        static bool StopMusic(MainMenuManager __instance, bool immediate)
        {
            MenuAudioProvider mainMenuCustomAudio = __instance.GetComponent<MenuAudioProvider>();
            mainMenuCustomAudio?.StopMusic();
            return false;
        }
    }
}
