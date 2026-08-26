using FGTools.LocalServer.CustomMessages.Logic;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Mediatonic.Networking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static FGTools.LocalServer.CustomMessages.Logic.CustomMessageManager;

namespace FGTools.LocalServer.CustomMessages
{
    internal class GMC_ServerConnectionStatus : FLZMessage
    {
        public GMC_ServerConnectionStatus() : base(FLZ_CustomMessage.FLZ_SERVER_CONNECTION_STATUS)
        {
        }

        public enum ServerResponse
        {
            AUTHENTICATION_REQUIRED,
            PLAYER_INFO_REQUIRED,
            JOIN_VERSION_OUTDATED,
            HOST_VERSION_OUTDATED,
            VERSION_DIFFERENCE
        }

        public ServerResponse Status { get; set; }

        internal override void Deserealize(NetworkReader netReader)
        {
            Status = (ServerResponse)netReader.ReadByte();
        }

        internal override Il2CppSystem.ArraySegment<byte> Serialize(NetworkWriter netWriter)
        {
            netWriter ??= new();

            netWriter.Write((byte)lvl);
            netWriter.Write((byte)Status);

            var segment = netWriter.AsArraySegment();
            var arr = new Il2CppStructArray<byte>(segment.Count);

            for (int i = 0; i < segment.Count; i++)
                arr[i] = segment.Array[segment.Offset + i];

            return new Il2CppSystem.ArraySegment<byte>(arr, 0, arr.Length);
        }
    }
}
