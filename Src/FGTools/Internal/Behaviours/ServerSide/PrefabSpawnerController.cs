using BepInEx.Logging;
using FG.Common;
using FGTools.Internal.Extensions;
using Levels.Obstacles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace FGTools.Internal.Behaviours
{
    //not proud of this 
    internal class PrefabSpawnerController : ToolsBehaviour
    {
        internal COMMON_PrefabSpawnerBase SpawnerBase;
        internal List<MPGNetID> SpawnRequests = [];
        void Awake()
        {
            SpawnerBase = GetComponent<COMMON_PrefabSpawnerBase>();
            if (SpawnerBase == null)
            {
                FLZ_Extensions.FGTLog(LogLevel.Error, GetType(), $"{name} lacks prefab spawner!!!");
                GameObject.Destroy(this);
                return;
            }

            SpawnRequests = [];
            GameActions.OnNetObjSpawned += OnNetObjSpawned;
        }

        void OnDestroy()
        {
            GameActions.OnNetObjSpawned -= OnNetObjSpawned;
        }

        void OnNetObjSpawned(MPGNetID netId, GameObject obj, int prefabHash)
        {
            if (!SpawnRequests.Contains(netId))
                return;

            foreach (var slop in SpawnerBase._spawnObjects)
            {
                if (GetEntryHash(slop.value) == prefabHash)
                {
                    SpawnRequests.Remove(netId);
                    obj.GetComponent<ServerControlledObject>().LifeTime = 15f;
                    SpawnerBase.OnInstantiateObject(obj, slop);
                    break;
                }
            }
        }

        static int GetEntryHash(GameObject go)
        {
            if (go.TryGetComponent<MPGNetObjectBootstrapper>(out var strapper))
            {
                return strapper.IdentifyingHash();
            }
            else
            {
                if (!go.TryGetComponent<MPGNetObject>(out var netObj))
                {
                    return -1;
                }

                return netObj.GenerateGameObjectHash(NetObjectCreationMode.Spawn);
            }
        }
    }
}
