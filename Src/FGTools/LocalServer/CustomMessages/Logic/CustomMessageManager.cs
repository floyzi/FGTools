using FG.Common;
using FGClient;
using FGTools.Services;
using Mediatonic.Networking;
using System;
using static FGTools.Internal.Extensions.FLZ_Extensions;

namespace FGTools.LocalServer.CustomMessages.Logic
{
    internal class CustomMessageManager
    {
        public enum FLZ_CustomMessage
        {
            FLZ_GENERIC_USER_INFO = 200,
            FLZ_CUSTOM_CONNECT_REQUEST = 201,
            FLZ_SERVER_CONNECTION_STATUS = 202,
        }

        internal void OnCustomMessageReceived(NetworkMessage msg)
        {
            var msgType = msg.reader._buf._buffer[0];
            var firstValue = (int)Enum.GetValues(typeof(FLZ_CustomMessage)).GetValue(0);

            if (msgType < firstValue)
            {
                FGTLog(BepInEx.Logging.LogLevel.Error, GetType(), $"Received unknown custom message? Received message type is {msgType} while we expected type >= to {firstValue}");
                return;
            }

            var flzMsg = (FLZ_CustomMessage)msgType;
            FGTLog(BepInEx.Logging.LogLevel.Info, GetType(), $"Received CUSTOM message of type \"{flzMsg}\"");

            switch (flzMsg)
            {
                case FLZ_CustomMessage.FLZ_SERVER_CONNECTION_STATUS:
                    CustomMessageDespatcher.Process(DeserealizeMessage<GMC_ServerConnectionStatus>(msg.reader));
                    break;
                case FLZ_CustomMessage.FLZ_CUSTOM_CONNECT_REQUEST:
                    CustomMessageDespatcher.Process(DeserealizeMessage<GMC_ClientConnectRequest>(msg.reader));
                    break;
                case FLZ_CustomMessage.FLZ_GENERIC_USER_INFO:
                    CustomMessageDespatcher.Process(DeserealizeMessage<GMC_ClientUserInfo>(msg.reader));
                    break;
            }

        }

        static T DeserealizeMessage<T>(NetworkReader netReader) where T : FLZMessage, new()
        {
    
            var msg = new T();
            try
            {
                msg.Deserealize(netReader);
            }
            catch (Exception ex)
            {
                FGTLog(BepInEx.Logging.LogLevel.Error, typeof(CustomMessageManager), $"Unable to deserealize message of type {msg.GetType().Name}. Bytes {netReader._buf.Length}\n\n{ex}");
            }
            return msg;
        }

        static Il2CppSystem.ArraySegment<byte> SerializeMessage(FLZMessage msg, out NetworkWriter writer)
        {
            writer = new NetworkWriter();
            try
            {
                return msg.Serialize(writer);
            }
            catch (Exception ex)
            {
                FGTLog(BepInEx.Logging.LogLevel.Error, typeof(CustomMessageManager), $"Unable to serialize message of type {msg.GetType().Name}. Bytes {writer._buffer.Length}\n\n{ex}");
                return new();
            }
        }

        public void SendMessageToClient<T>(T msg, GameConnection conn) where T : FLZMessage
        {
            conn.SendReliable(SerializeMessage(msg, out var writer));
            FGTLog(BepInEx.Logging.LogLevel.Info, GetType(), $"Sent message \"{msg.GetType().Name}\" (id {(byte)msg.lvl}) to \"{conn.RemoteNetworkID}\". Sent bytes {writer._buffer._count}");
        }

        public void SendMessageToServer<T>(T msg) where T : FLZMessage
        {
            GlobalGameStateClient.Instance.NetworkManager.ConnectionToServer.SendReliable(SerializeMessage(msg, out var writer));
            FGTLog(BepInEx.Logging.LogLevel.Info, GetType(), $"Sent message \"{msg.GetType().Name}\" (id {(byte)msg.lvl}) to the server. Sent bytes {writer._buffer._count}");
        }
    }
}