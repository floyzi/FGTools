using FG.Common;
using FGClient;
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
    internal class GMC_ClientConnectRequest : FLZMessage
    {
        public GMC_ClientConnectRequest() : base(FLZ_CustomMessage.FLZ_CUSTOM_CONNECT_REQUEST)
        {
        }

        public string Version { get; set; }
        public Guid ID { get; set; }
        public string Commit { get; set; }
        public DateTime Date { get; set; }
        public FG_NetworkID NetworkID { get; set; }

        internal override void Deserealize(NetworkReader netReader)
        {
            Version = netReader.ReadString();
            ID = Guid.Parse(netReader.ReadString());
            Commit = netReader.ReadString();
            Date = DateTime.Parse(netReader.ReadString());
            NetworkID = new(netReader.ReadUInt64());
        }

        internal override Il2CppSystem.ArraySegment<byte> Serialize(NetworkWriter netWriter)
        {
            netWriter ??= new();

            netWriter.Write((byte)lvl);
            netWriter.Write(Launcher.BuildInfo.Version);
            netWriter.Write(Launcher.BuildInfo.GUID.ToString());
            netWriter.Write(Launcher.BuildInfo.GetCommit());
            netWriter.Write(Launcher.BuildInfo.BuildDate.ToString());
            netWriter.Write(GlobalGameStateClient.Instance.GetLocalClientNetworkID().NetworkID);

            var segment = netWriter.AsArraySegment();
            var arr = new Il2CppStructArray<byte>(segment.Count);

            for (int i = 0; i < segment.Count; i++)
                arr[i] = segment.Array[segment.Offset + i];

            return new Il2CppSystem.ArraySegment<byte>(arr, 0, arr.Length);
        }
    }
}
