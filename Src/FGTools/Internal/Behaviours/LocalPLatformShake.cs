using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using FGTools.States.Logic;
using Il2CppInterop.Runtime.Attributes;
using Levels.TipToe;
using UnityEngine;

namespace FGTools.Internal.Behaviours
{
    internal class LocalPLatformShake : ToolsBehaviour
    {
        public TipToe_PlatformController ttpc;
        bool allowShake = true;
        [HideFromIl2Cpp]
        IEnumerator ShakePlatform(float delay, int platformsToShake)
        {
            List<int> list = [];
            for (int i = 0; i < platformsToShake; i++)
            {
                int rand = UnityEngine.Random.Range(0, ttpc._tipToePlatform.Count);
                if (!list.Contains(rand))
                    list.Add(rand);
                else
                    i--;
            }

            foreach (int indx in list)
            {
                if (ttpc._tipToePlatform[indx].IsFakePlatform)
                    ttpc.ServerTriggerPlatformShake(indx);
            }

            allowShake = false;
            yield return new WaitForSeconds(delay);
            allowShake = true;
        }

        private void Update()
        {
            if (ttpc != null && allowShake && FGTStateManager.StateManager.FGTCurrentState == FGTStateManager.FGTState.GameActive)
            {
                if (ttpc._tipToePlatform.Count < 500)
                    StartCoroutine(ShakePlatform(0.35f, UnityEngine.Random.Range(2, 10)).WrapToIl2Cpp());
                else
                    StartCoroutine(ShakePlatform(0.25f, UnityEngine.Random.Range(10, 25)).WrapToIl2Cpp());
            }
        }
    }
}
