using BepInEx.Unity.IL2CPP.Utils.Collections;
using FG.Common;
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

namespace FGTools.Internal.Behaviours
{
    internal class RespawnTileController : ToolsBehaviour
    {
        public bool allowDespawnAtStep = false;
        float timeUntilDespawn = 0;

        [HideFromIl2Cpp]
        private IEnumerator RespawnPlatform(float t)
        {
            yield return new WaitForSeconds(t);
            tile.OnTriggerRespawnRoutine();
        }

        void Update()
        {
            if (timeUntilDespawn != -1 && tile._despawningStrategy == DespawnPlatformStrategy.DespawnAfterTime)
            {
                timeUntilDespawn += Time.deltaTime;

                if (timeUntilDespawn >= tile._despawnTimer)
                {
                    tile.HandleTileTriggerDespawning();

                    if (tile._respawningStrategy == RespawnPlatformStrategy.RespawnAfterTime)
                        tile.StartCoroutine(tile.RespawnTileAfterDespawnWithTimerStrategy());
                    else
                        timeUntilDespawn = -1;
                }
            }
        }
        public void OnTriggerEnter(Collider collider)
        {
            if (allowDespawnAtStep)
            {
                var a = collider.gameObject.GetComponent<MPGNetObject>();
                if (a != null && a.IsFallGuy)
                {
                    tile.HandleTileTriggerDespawning();
                    StartCoroutine(RespawnPlatform(tile._brokenDuration).WrapToIl2Cpp());
                }
            }
        }

        public COMMON_RespawningTile tile;
    }
}
