using System;
using System.Collections.Generic;
using FG.Common;
using FGClient;
using Il2CppInterop.Runtime.Injection;
using UniverseLib;
using static FGTools.Internal.Extensions.FLZ_Extensions;

namespace FGTools.LocalServer
{
    public class ServerReader : ClientNetworkMessageProcessor
    {
        public ServerReader(IntPtr pointer) : base(pointer)
        {

        }

        public ServerReader() : base(ClassInjector.DerivedConstructorPointer<ServerReader>())
        {
            ClassInjector.DerivedConstructorBody(this);
        }

        public List<GameMessageBase> msgs = new();

        public override void processMessage(GameConnection pFrom, GameMessageBase msg)
        {
            var msgType = msg.getGameMessageType();

            if (msgType != EnumGameMessageType.GMT_PING && msgType != EnumGameMessageType.GMT_CLIENT_MOTORTASKS)
                FGTLog(BepInEx.Logging.LogLevel.Warning, base.GetType(), $"Server got message of type {msgType} {msg.GetActualType().Name}");

            msgs.Add(msg);

            switch (msgType)
            {
                case EnumGameMessageType.GMT_PING:
                    ServerDespatcher.Process(msg.Cast<GameMessagePing>(), pFrom);
                    break;
                case EnumGameMessageType.GMT_CLIENT_CONNECT_INITIAL:
                    ServerDespatcher.Process(msg.Cast<GameMessageClientConnectInitial>(), pFrom);
                    break;
                case EnumGameMessageType.GMT_CLIENT_CONNECT_CLIENT:
                    ServerDespatcher.Process(msg.Cast<GameMessageBasePublicICopyable1ObfInUIInObFGInStBoInByUnique>(), pFrom);
                    break;
                case EnumGameMessageType.GMT_CLIENT_SET_READY:
                    ServerDespatcher.Process(msg.Cast<GameMessageClientSetReady>(), pFrom);
                    break;
                case EnumGameMessageType.GMT_CLIENT_REQUEST_SPAWN_PLAYER:
                    ServerDespatcher.Process(msg.Cast<GameMessageClientRequestSpawnPlayer>(), pFrom);
                    break;
                case EnumGameMessageType.GMT_CLIENT_MOTORTASKS:
                    ServerDespatcher.Process(msg.Cast<GameMessageClientMotorTasks>(), pFrom);
                    break;
                case EnumGameMessageType.GMT_CLIENT_DISCONNECT_PLAYER:
                    ServerDespatcher.Process(msg.Cast<GameMessageClientDisconnectPlayer>(), pFrom);
                    break;
                case EnumGameMessageType.GMT_CLIENT_TIMEATTACK_RESPAWN:
                    ServerDespatcher.Process(msg.Cast<GameMessageClientTimeAttackReset>(), pFrom);
                    break;
                case EnumGameMessageType.GMT_CLIENT_RESET_TO_CHECKPOINT:
                    ServerDespatcher.Process(msg.Cast<GameMessageClientResetToCheckpoint>(), pFrom);
                    break;
                case EnumGameMessageType.GMT_CLIENT_SKIP_ROUND:
                    ServerDespatcher.Process(msg.Cast<GameMessageClientSkipRound>(), pFrom);
                    break;
                default:
                    FGTLog(BepInEx.Logging.LogLevel.Error, base.GetType(), $"Unhandled message of type {msg._gmt}");
                    break;
            }
        }

        public override void handleDisconnection(GameConnection netConn, bool hasInternalError)
        {
            msgs.Clear();
        }
    }
}
