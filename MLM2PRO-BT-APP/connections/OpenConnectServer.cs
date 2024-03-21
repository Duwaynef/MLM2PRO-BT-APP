using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Windows;
using MLM2PRO_BT_APP.util;
using NetCoreServer;

namespace MLM2PRO_BT_APP.connections
{
    internal class OpenConnectServerSession : TcpSession
    {
        public OpenConnectServerSession(TcpServer server) : base(server) { }

        protected override void OnConnected()
        {
            Logger.Log($"OpenConnectServer: TCP session with Id {Id} connected!");
            (Application.Current as App)?.Dispatcher.Invoke(() => (Application.Current as App)?.SendOpenConnectServerNewClientMessage());
            Logger.Log($"OpenConnectServer: Sent opening messages");
        }

        protected override void OnDisconnected()
        {
            Logger.Log($"OpenConnectServer: disconnected {Id}");
        }
        
        protected override void OnReceived(byte[] buffer, long offset, long size)
        {
            Logger.Log($"OpenConnectServer: received {size} bytes");
            string? message = Encoding.UTF8.GetString(buffer, (int)offset, (int)size);
            (Application.Current as App)?.Dispatcher.Invoke(() => (Application.Current as App)?.RelayOpenConnectServerMessage(message));
        }

        protected override void OnError(SocketError error)
        {
            Logger.Log($"OpenConnectServer: caught an error with code {error}");
        }
    }
    class OpenConnectServer : TcpServer
    {
        public OpenConnectServer(IPAddress address, int port) : base(IPAddress.Any, SettingsManager.Instance.Settings.OpenConnect.APIRelayPort) { }

        protected override TcpSession CreateSession() { return new OpenConnectServerSession(this); }

        protected override void OnError(SocketError error)
        {
            Logger.Log($"OpenConnectServer: Chat TCP server caught an error with code {error}");
        }
    }
}
