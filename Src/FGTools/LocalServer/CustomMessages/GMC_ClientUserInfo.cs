using FG.Common;
using FG.Common.ExtensionMethods;
using FGClient;
using FGTools.LocalServer.CustomMessages.Logic;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppSystem;
using Mediatonic.Networking;
using MPG.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using UnityEngine.InputSystem;
using static FGTools.LocalServer.CustomMessages.Logic.CustomMessageManager;
using static UnityEngine.ResourceManagement.Util.BinaryStorageBuffer;

namespace FGTools.LocalServer.CustomMessages
{
    internal class GMC_ClientUserInfo : FLZMessage
    {
        public GMC_ClientUserInfo() : base(FLZ_CustomMessage.FLZ_GENERIC_USER_INFO)
        {
        }

        public struct WheelElement
        {
            public uint Id;
            public string Type;
        }

        public string Username { get; set; }
        public string AccountID { get; set; }
        public string Platform { get; set; }
        public FG_NetworkID NetworkID { get; set; }
        public int CrownRank { get; set; }
        public uint Color { get; set; }
        public uint Pattern { get; set; }
        public uint Faceplate { get; set; }
        public uint UpperCostume { get; set; }
        public uint LowerCostume { get; set; }
        public uint Nameplate { get; set; }
        public uint Nickname { get; set; }
        public uint VictoryPose { get; set; }
        public Dictionary<int, WheelElement> FirstWheel { get; set; }
        public Dictionary<int, WheelElement> SecondWheel { get; set; }

        internal override Il2CppSystem.ArraySegment<byte> Serialize(NetworkWriter netWriter)
        {
            var s = GlobalGameStateClient.Instance.PlayerProfile.CustomisationSelections;

            FirstWheel = s.FirstWheelOptions.Take(8).Select((opt, indx) => new { 
                indx, 
                element = new WheelElement
                {
                    Id = Crc32.Calc(opt.ItemId),
                    Type = opt.CMSGroupID.ToString()
                }
            }).ToDictionary(x => x.indx, x => x.element);

            SecondWheel = s.SecondWheelOptions.Take(8).Select((opt, indx) => new {
                indx,
                element = new WheelElement
                {
                    Id = Crc32.Calc(opt.ItemId),
                    Type = opt.CMSGroupID.ToString()
                }
            }).ToDictionary(x => x.indx, x => x.element);

            netWriter ??= new();

            netWriter.Write((byte)lvl);
            netWriter.Write(GlobalGameStateClient.Instance.GetLocalPlayerName());
            netWriter.Write(GlobalGameStateClient.Instance.GetLocalClientAccountID());
            netWriter.Write(ClientBuildDetails.Platform);
            netWriter.Write(GlobalGameStateClient.Instance.GetLocalClientNetworkID().NetworkID);
            netWriter.Write(-1);
            netWriter.Write(Crc32.Calc(s.ColourOption.ItemId));
            netWriter.Write(Crc32.Calc(s.PatternOption.ItemId));
            netWriter.Write(Crc32.Calc(s.FaceplateOption.ItemId));
            netWriter.Write(Crc32.Calc(s.CostumeTopOption.ItemId));
            netWriter.Write(Crc32.Calc(s.CostumeBottomOption.ItemId));
            netWriter.Write(Crc32.Calc(s.NameplateOption.ItemId));
            netWriter.Write(Crc32.Calc(s.NicknameOption.ItemId));
            netWriter.Write(Crc32.Calc(s.VictoryPoseOption.ItemId));

            if (FirstWheel == null)
                netWriter.Write((ushort)0);
            else
            {
                netWriter.Write((ushort)FirstWheel.Count);
                foreach (var kvp in FirstWheel)
                {
                    netWriter.Write(kvp.Key);
                    netWriter.Write(kvp.Value.Id);
                    netWriter.Write(kvp.Value.Type);
                }
            }


            if (SecondWheel == null)
                netWriter.Write((ushort)0);
            else
            {
                netWriter.Write((ushort)SecondWheel.Count);
                foreach (var kvp in SecondWheel)
                {
                    netWriter.Write(kvp.Key);
                    netWriter.Write(kvp.Value.Id);
                    netWriter.Write(kvp.Value.Type);
                }
            }

            var segment = netWriter.AsArraySegment();
            var arr = new Il2CppStructArray<byte>(segment.Count);

            for (int i = 0; i < segment.Count; i++)
                arr[i] = segment.Array[segment.Offset + i];

            return new Il2CppSystem.ArraySegment<byte>(arr, 0, arr.Length);
        }

        internal override void Deserealize(NetworkReader netReader)
        {
            Username = netReader.ReadString();
            AccountID = netReader.ReadString();
            Platform = netReader.ReadString();
            NetworkID = new(netReader.ReadUInt64());
            CrownRank = netReader.ReadInt32();
            Color = netReader.ReadUInt32();
            Pattern = netReader.ReadUInt32();
            Faceplate = netReader.ReadUInt32();
            UpperCostume = netReader.ReadUInt32();
            LowerCostume = netReader.ReadUInt32();
            Nameplate = netReader.ReadUInt32();
            Nickname = netReader.ReadUInt32();
            VictoryPose = netReader.ReadUInt32();

            ushort firstWheel = netReader.ReadUInt16();
            FirstWheel = new Dictionary<int, WheelElement>(firstWheel);
            for (int i = 0; i < firstWheel; i++)
                FirstWheel[netReader.ReadInt32()] = new WheelElement { Id = netReader.ReadUInt32(), Type = netReader.ReadString() };

            ushort secondWheel = netReader.ReadUInt16();
            SecondWheel = new Dictionary<int, WheelElement>(secondWheel);
            for (int i = 0; i < secondWheel; i++)
                SecondWheel[netReader.ReadInt32()] = new WheelElement { Id = netReader.ReadUInt32(), Type = netReader.ReadString() };
        }

        internal CustomisationSelections CreateSelections()
        {
            var res = new CustomisationSelections();
            var m = CustomisationManager.Instance;

            res.FirstWheelOptions = new(8);
            res.SecondWheelOptions = new(8);

            res.ColourOption = m.GetColourOptionWithId(Color, true);
            res.PatternOption = m.GetSkinPatternOptionWithId(Pattern, true);
            res.FaceplateOption = m.GetFaceplateOptionWithId(Faceplate, true);
            res.CostumeTopOption = m.GetUpperCostumeWithId(UpperCostume, true);
            res.CostumeBottomOption = m.GetLowerCostumeWithId(LowerCostume, true);
            res.NicknameOption = m.GetNicknameOptionWithId(Nickname, true);
            res.NameplateOption = m.GetNameplateOptionWithId(Nameplate, true);
            res.VictoryPoseOption = m.GetVictoryOptionWithId(VictoryPose, true);

            if (FirstWheel != null)
            {
                for (int i = 0; i < FirstWheel.Count; i++)
                {
                    var elem = FirstWheel.ElementAt(i);
                    ItemDefinitionSO itmRes = null;

                    switch (elem.Value.Type?.ToString())
                    {
                        case "cosmetics_emotes":
                            itmRes = m.GetEmoteOptionsWithId(elem.Value.Id, true);
                            break;
                        case "cosmetics_emoticons":
                            itmRes = m.GetEmoticonOptionWithId(elem.Value.Id, true);
                            break;
                        case "cosmetics_phrases":
                            itmRes = m.GetPhraseOptionWithId(elem.Value.Id, true);
                            break;
                    }

                    res.FirstWheelOptions[i] = itmRes;
                }
            }

            if (SecondWheel != null)
            {
                for (int i = 0; i < SecondWheel.Count; i++)
                {
                    var elem = SecondWheel.ElementAt(i);
                    ItemDefinitionSO itmRes = null;

                    switch (elem.Value.Type?.ToString())
                    {
                        case "cosmetics_emotes":
                            itmRes = m.GetEmoteOptionsWithId(elem.Value.Id, true);
                            break;
                        case "cosmetics_emoticons":
                            itmRes = m.GetEmoticonOptionWithId(elem.Value.Id, true);
                            break;
                        case "cosmetics_phrases":
                            itmRes = m.GetPhraseOptionWithId(elem.Value.Id, true);
                            break;
                    }

                    res.SecondWheelOptions[i] = itmRes;
                }
            }

            return res;
        }
    }

}
