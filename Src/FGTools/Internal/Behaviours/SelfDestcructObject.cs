using BepInEx.Unity.IL2CPP.Utils.Collections;
using FG.Common;
using FGClient;
using FGTools.Services;
using FGTools.States.Logic;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using static FGTools.LocalServer.ServerManager;

namespace FGTools.Internal.Behaviours
{
    internal class SelfDestcructObject : ToolsBehaviour
    {
        NetObjectSpawnData SpawnData;
        internal void Init(NetObjectSpawnData spawnData)
        {
            SpawnData = spawnData ?? throw new ArgumentException("Soawn data was null, this is not intended");

            if (spawnData.SelfDestructionTime == 0)
                throw new ArgumentException("Destroy time was 0, this is not intended");

            Invoke("SelfDestroy", spawnData.SelfDestructionTime);
        }

        internal void SelfDestroy()
        {
            MPGNetObject obj = null;
            if (GlobalGameStateClient.Instance.NetObjectManager.TryGetNetObject(SpawnData.NetID, out obj))
                ServerGameStateActions.Instance.RequestDestroy(obj);
        }
    }
}
