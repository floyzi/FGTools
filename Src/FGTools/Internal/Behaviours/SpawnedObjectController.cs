using BepInEx.Unity.IL2CPP.Utils.Collections;
using FGTools.States.Logic;
using Il2CppInterop.Runtime.Attributes;
using Levels.Obstacles;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using static FGTools.Config.ConfigManager;

namespace FGTools.Internal.Behaviours
{
    internal class SpawnedObjectController : FGTBehaviour
    {
        bool _limitedLifeTime = true;
        int index;
        COMMON_SpawnBasket spawnBasket;
        //Vector3 spawnPos;
        //Quaternion spawnRot;
        public static string[] _drs = { "FallGuy_SnowballSurvival", "FallGuy_FallBall_5" };

        public void Load(COMMON_SpawnBasket basket, int basketIndex, bool limitedLife)
        {
            spawnBasket = basket;
            index = basketIndex;
            _limitedLifeTime = limitedLife;
        }
        void Update()
        {
            if (_limitedLifeTime)
                StartCoroutine(destroyAfter(SpawnedObjLifeTime.Value).WrapToIl2Cpp());

            if (gameObject.transform.position.y < -75f)
            {
                if (spawnBasket == null)
                    Destroy(gameObject);
                else
                {
                    var target = spawnBasket._spawnedItemGOs[index];
                    if (target != null)
                        spawnBasket.TryReturnCarryObjectToPool(target);
                }
            }
        }

        [HideFromIl2Cpp]
        IEnumerator destroyAfter(int sec)
        {
            _limitedLifeTime = true;
            yield return new WaitForSeconds(sec);
            Destroy(gameObject);
        }
    }
}
