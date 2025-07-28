using FGClient;
using FGTools.Services;
using FGTools.Services.Logic;
using FGTools.States.Logic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace FGTools.Internal.Behaviours
{
    internal abstract class ToolsBehaviour : MonoBehaviour
    {
        internal static FGTStateManager StateManager => FGTBase.StateManager;
        internal static ClientGameManager CGM => FGTBase.CGM;
        internal static FallGuyBehaviour FGBehaviour => FGTBase.FGBehaviour;
        internal static FGTServiceManager FGTServiceManager => FGTBase.FGTServiceManager;
        internal static RoundLoaderService FGTRoundLoader => FGTBase.FGTRoundLoader;
        internal static OnlineCheckService OnlineCheck => FGTBase.OnlineCheck;
    }
}
