using FG.Common;
using FGClient;
using FGClient.CatapultServices;
using FGClient.Challenges;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static CatapultAnalytics;

namespace FGTools.HarmonyPatches
{
    public class OfflineOnlyPatches
    {
        [HarmonyPatch(typeof(CatapultServicesManager), "HandleLoginFailure")]
        [HarmonyPatch(typeof(CatapultServicesManager), "HandleGaveUpTryingToReconnect")]
        [HarmonyPatch(typeof(ChallengesManager), "TryUpdatePlayerChallengeGroups")]
        [HarmonyPatch(typeof(EpicAccountsHelper), "EOSLogin")]
        [HarmonyPrefix]
        static bool thisShouldBeEmptyBecauseIWant()
        {
            return false;
        }
    }
}
