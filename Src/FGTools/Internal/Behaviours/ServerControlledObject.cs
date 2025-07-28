using FG.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace FGTools.Internal.Behaviours
{
    internal class ServerControlledObject : ServerBehaviour
    {
        void Update()
        {
            if (TryGetComponent<FallGuysCharacterController>(out var fgcc) && !fgcc.IsLocalPlayer)
                transform.GetComponent<Rigidbody>().detectCollisions = true;

            if (transform.position.y < GetRespawnPos())
            {
                var pos = GetRespawnLocation();
                ServerGameStateActions.Instance.TeleportNetObject(transform.GetComponent<MPGNetObject>(), pos.Position, pos.Rotation, LiveOps.Challenges.SpawnReason.Respawn);
            }
        }
    }
}
