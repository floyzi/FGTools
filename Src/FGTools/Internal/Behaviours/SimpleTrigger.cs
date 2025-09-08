using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace FGTools.Internal.Behaviours
{
    internal class SimpleTrigger : ToolsBehaviour
    {
        internal Action<Collider> TriggerEnter;
        internal Action<Collision> CollisionEnter;
        void OnTriggerEnter(Collider other)
        {
            TriggerEnter?.Invoke(other);
        }

        void OnCollisionEnter(Collision other)
        {
            CollisionEnter?.Invoke(other);
        }
    }
}
