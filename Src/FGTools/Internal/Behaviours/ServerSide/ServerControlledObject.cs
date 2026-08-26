using FG.Common;
using FGTools.Internal.Extensions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace FGTools.Internal.Behaviours.ServerSide
{
    internal class ServerControlledObject : ServerBehaviour
    {
        Stopwatch LifeTimeWatch = new();
        internal float LifeTime = -1f;

        void OnEnable()
        {
            LifeTimeWatch = Stopwatch.StartNew();
        }

        void Update()
        {
            if (!TryGetComponent<MPGNetObject>(out var mpg))
            {
                FLZ_Extensions.FGTLog(BepInEx.Logging.LogLevel.Error, GetType(), $"ServerControlledObject lacks MPGNetObject!!! Obj={name}");
                return;
            }

            if (TryGetComponent<FallGuysCharacterController>(out var fgcc) && !fgcc.IsLocalPlayer)
                transform.GetComponent<Rigidbody>().detectCollisions = true;

            if (LifeTime != -1 && LifeTimeWatch.IsRunning && LifeTimeWatch.Elapsed.Seconds > LifeTime)
            {
                Despawn(mpg);
                LifeTimeWatch.Stop();
                return;
            }

            if (transform.position.y < GetRespawnPos())
            {
                var pos = GetRespawnLocation();

                if (mpg.IsFallGuy)
                    ServerGameStateActions.Instance.TeleportNetObject(mpg, pos.Position, pos.Rotation, LiveOps.Challenges.SpawnReason.Respawn);
                else
                    Despawn(mpg);
            }
        }

        void Despawn(MPGNetObject netObj)
        {
            if (netObj == null)
                return;

            if (netObj.NetID == default)
            {
                FLZ_Extensions.FGTLog(BepInEx.Logging.LogLevel.Error, GetType(), $"ServerControlledObject using default NetID during despawn!!! Obj={name}");
                GameObject.Destroy(gameObject);
                return;
            }

            ServerGameStateActions.Instance.RequestDestroy(netObj);
        }
    }
}
