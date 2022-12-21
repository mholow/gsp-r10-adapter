using System;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using TcpClient = NetCoreServer.TcpClient;

namespace gspro_r10
{
    class OpenConnectClient : TcpClient
    {
        public Timer? PingTimer { get; private set; }

        public OpenConnectClient(string address, int port) : base(address, port) {}

        public void DisconnectAndStop()
        {
            _stop = true;
            DisconnectAsync();
            while (IsConnected)
                Thread.Yield();
        }

        protected override void OnConnected()
        {
            InitiallyConnected = true;
            SimpleLogger.LogGSPInfo($"TCP client connected a new session with Id {Id}");
            PingTimer = new Timer(SendPing, null, 0, 0);
        }

        private void SendPing(object? state)
        {
            SendAsync(JsonSerializer.Serialize(OpenConnect.OpenConnectApiMessage.CreateHeartbeat()));
        }

        public override bool ConnectAsync()
        {
            SimpleLogger.LogGSPInfo($"Connecting to OpenConnect api ({Address}:{Port})...");
            return base.ConnectAsync();
        }

        public override bool SendAsync(string message)
        {
            SimpleLogger.LogGSPOutgoing(message);
            return base.SendAsync(message);
        }

        protected override void OnDisconnected()
        {
            if (InitiallyConnected)
                SimpleLogger.LogGSPError($"TCP client disconnected a session with Id {Id}");

            Thread.Sleep(5000);
            if (!_stop)
                ConnectAsync();
        }

        protected override void OnReceived(byte[] buffer, long offset, long size)
        {
            string received = Encoding.UTF8.GetString(buffer, (int)offset, (int)size);
            SimpleLogger.LogGSPIncoming(received);
        }

        protected override void OnError(SocketError error)
        {
            if (error != SocketError.TimedOut)
                SimpleLogger.LogGSPError($"TCP client caught an error with code {error}");
        }

        private bool _stop;

    public bool InitiallyConnected { get; private set; }
  }
}