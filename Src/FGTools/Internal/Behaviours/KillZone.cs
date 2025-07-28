using FGTools.Config;
using FGTools.States;
using FGTools.States.Logic;
using UnityEngine;

namespace FGTools.Internal.Behaviours
{
    internal class KillZone : ToolsBehaviour
    {
        void PerfomAction()
        {
            if (CGM._round.Archetype.Id.Contains("race") || CGM._round.Archetype.Id.Contains("points"))
                FallGuyBehaviour._instance.RespawnPlayer();
            else if (CGM._round.Archetype.Id.Contains("survival") && ConfigManager.ElimLevel.Value > ConfigManager.ElimType.None)
                StateManager.GetState<GameplayState>().DoElim();
        }
        public void OnTriggerEnter(Collider collider)
        {
            PerfomAction();
        }

        public void OnCollisionEnter(Collision collision)
        {
            PerfomAction();
        }
    }
}
