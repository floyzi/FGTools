using FG.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FGTools.LocalServer.CustomMessages.Logic
{
    internal static class CustomMessageDespatcher
    {
        internal static event Action<GMC_ClientConnectRequest> OnServerConnectRequest;
        internal static event Action<GMC_ClientUserInfo> OnServerUserInfo;
        internal static event Action OnClientSendAuthRequest;
        internal static event Action OnClientSendUserInfo;
        internal static event Action OnClientOutdatedHost;
        internal static event Action OnClientOutdatedClient;
        internal static event Action OnClientVersionDifference;

        internal static void Process(GMC_ServerConnectionStatus msg)
        {
            switch (msg.Status)
            {
                case GMC_ServerConnectionStatus.ServerResponse.AUTHENTICATION_REQUIRED:
                    OnClientSendAuthRequest?.Invoke();
                    break;
                case GMC_ServerConnectionStatus.ServerResponse.PLAYER_INFO_REQUIRED:
                    OnClientSendUserInfo?.Invoke();
                    break;
                case GMC_ServerConnectionStatus.ServerResponse.HOST_VERSION_OUTDATED:
                    OnClientOutdatedHost?.Invoke();
                    break;
                case GMC_ServerConnectionStatus.ServerResponse.JOIN_VERSION_OUTDATED:
                    OnClientOutdatedClient?.Invoke();
                    break;
                case GMC_ServerConnectionStatus.ServerResponse.VERSION_DIFFERENCE:
                    OnClientVersionDifference?.Invoke();
                    break;
                default: throw new NotImplementedException($"GMC_ServerConnectionStatus STATE {msg.Status} NOT IMPLEMENTED!");
            }
        }
        internal static void Process(GMC_ClientConnectRequest msg)
        {
            OnServerConnectRequest?.Invoke(msg);
        }
        internal static void Process(GMC_ClientUserInfo msg)
        {
            OnServerUserInfo?.Invoke(msg);
        }
    }
}
