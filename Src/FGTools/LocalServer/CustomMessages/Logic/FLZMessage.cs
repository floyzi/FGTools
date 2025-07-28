using FG.Common;
using Mediatonic.Networking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static FGTools.LocalServer.CustomMessages.Logic.CustomMessageManager;

namespace FGTools.LocalServer.CustomMessages
{
    internal abstract class FLZMessage(FLZ_CustomMessage lvl)
    {
        internal FLZ_CustomMessage lvl = lvl;
        internal abstract Il2CppSystem.ArraySegment<byte> Serialize(NetworkWriter netWriter);
        internal abstract void Deserealize(NetworkReader netReader);
    }
}
