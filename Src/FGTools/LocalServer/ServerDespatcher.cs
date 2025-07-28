using System;
using FG.Common;
using MPG.Utility;

namespace FGTools.LocalServer
{
    public static class ServerDespatcher
    {
        public static event Action<GameMessagePing, GameConnection> OnPing;
        public static event Action<GameMessageClientMotorTasks, GameConnection> OnMotorTasks;
        public static event Action<GameMessageBasePublicICopyable1ObfInUIInObFGInStBoInByUnique, GameConnection> OnClientConnectClient;
        public static event Action<GameMessageClientConnectInitial, GameConnection> OnClientConnectInitial;
        public static event Action<GameMessageClientDisconnectPlayer, GameConnection> OnDisconnectPlayer;
        public static event Action<GameMessageClientSetReady, GameConnection> OnSetReady;
        public static event Action<GameMessageClientRequestSpawnPlayer, GameConnection> OnSpawnPlayer;
        public static event Action<GameMessageClientTimeAttackReset, GameConnection> OnTimeAttackReset;

        public static void Process(GameMessageClientRequestSpawnPlayer msg, GameConnection playerConn)
        {
            OnSpawnPlayer?.Invoke(msg, playerConn);
        }
        public static void Process(GameMessagePing msg, GameConnection playerConn)
        {
            OnPing?.Invoke(msg, playerConn);
        }
        public static void Process(GameMessageBasePublicICopyable1ObfInUIInObFGInStBoInByUnique msg, GameConnection playerConn)
        {
            OnClientConnectClient?.Invoke(msg, playerConn);
        }

        public static void Process(GameMessageClientConnectInitial msg, GameConnection playerConn)
        {
            OnClientConnectInitial?.Invoke(msg, playerConn);
        }

        public static void Process(GameMessageClientMotorTasks msg, GameConnection playerConn)
        {
            OnMotorTasks?.Invoke(msg, playerConn);
        }

        public static void Process(GameMessageClientDisconnectPlayer msg, GameConnection playerConn)
        {
            OnDisconnectPlayer?.Invoke(msg, playerConn);
        }

        public static void Process(GameMessageClientSetReady msg, GameConnection playerConn)
        {
            OnSetReady?.Invoke(msg, playerConn);
        }
        public static void Process(GameMessageClientTimeAttackReset msg, GameConnection playerConn)
        {
            OnTimeAttackReset?.Invoke(msg, playerConn);
        }
    }
}
