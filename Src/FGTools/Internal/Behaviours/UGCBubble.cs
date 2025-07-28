using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FG.Common;
using FGTools.States.Logic;
using Levels.Progression;
using UnityEngine;

namespace FGTools.Internal.Behaviours
{
    internal class UGCBubble : ToolsBehaviour
    {
        public COMMON_ScoringBubble bubble;
        void OnTriggerEnter(Collider collider)
        {
            CGM._soloScoreManager.AwardSoloPoints(FallGuyBehaviour._instance.FGMPG.NetID, bubble._pointsAwarded);
            gameObject.transform.GetChild(0).gameObject.GetComponent<VFXBeachballAnimation>().Collect();
        }
    }
}
