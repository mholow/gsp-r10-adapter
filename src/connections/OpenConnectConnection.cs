using System;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using gspro_r10.OpenConnect;
using Microsoft.Extensions.Configuration;
using TcpClient = NetCoreServer.TcpClient;

namespace gspro_r10
{
  class OpenConnectClient : TcpClient
  {
    public Timer? PingTimer { get; private set; }
    public bool InitiallyConnected { get; private set; }
    private bool _stop;

    private ConnectionManager _connectionManager;

    public OpenConnectClient(ConnectionManager connectionManager, IConfigurationSection configuration)
      : base(configuration["ip"] ?? "127.0.0.1", int.Parse(configuration["port"] ?? "921"))
    {
      _connectionManager = connectionManager;
    }

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
      OpenConnectLogger.LogGSPInfo($"TCP client connected a new session with Id {Id}");
      PingTimer = new Timer(SendPing, null, 0, 0);
    }

    private void SendPing(object? state)
    {
      SendAsync(JsonSerializer.Serialize(OpenConnect.OpenConnectApiMessage.CreateHeartbeat()));
    }

    public void SetDeviceReady(bool deviceReady)
    {
      SendAsync(JsonSerializer.Serialize(OpenConnect.OpenConnectApiMessage.CreateHeartbeat(deviceReady)));
    }

    public override bool ConnectAsync()
    {
      OpenConnectLogger.LogGSPInfo($"Connecting to OpenConnect api ({Address}:{Port})...");
      return base.ConnectAsync();
    }

    public override bool SendAsync(string message)
    {
      OpenConnectLogger.LogGSPOutgoing(message);
      return base.SendAsync(message);
    }

    protected override void OnDisconnected()
    {
      if (InitiallyConnected)
        OpenConnectLogger.LogGSPError($"TCP client disconnected a session with Id {Id}");

      Thread.Sleep(5000);
      if (!_stop)
        ConnectAsync();
    }

    protected override void OnReceived(byte[] buffer, long offset, long size)
    {
      string received = Encoding.UTF8.GetString(buffer, (int)offset, (int)size);
      OpenConnectLogger.LogGSPIncoming(received);

      OpenConnectInfoMessage m;
      try
      {
        m = JsonSerializer.Deserialize<OpenConnectInfoMessage>(received) ?? new OpenConnectInfoMessage();
      }
      catch (JsonException)
      {
        m = new OpenConnectInfoMessage();
      }

      switch (m.Code)
      {
        case Code.PlayerInfo:
          PlayerInformation info = JsonSerializer.Deserialize<PlayerInformation>(received) ?? new PlayerInformation();
          OpenConnectLogger.LogGSPInfo($"New Club {info.Club} Selected.");
          _connectionManager.ClubChanged(info.Club);
          break;
      };

    }

    protected override void OnError(SocketError error)
    {
      if (error != SocketError.TimedOut)
        OpenConnectLogger.LogGSPError($"TCP client caught an error with code {error}");
    }
  }

  public static class OpenConnectLogger
  {
    public static void LogGSPInfo(string message) => LogGSPMessage(message, LogMessageType.Informational);
    public static void LogGSPError(string message) => LogGSPMessage(message, LogMessageType.Error);
    public static void LogGSPOutgoing(string message) => LogGSPMessage(message, LogMessageType.Outgoing);
    public static void LogGSPIncoming(string message) => LogGSPMessage(message, LogMessageType.Incoming);
    public static void LogGSPMessage(string message, LogMessageType type) => BaseLogger.LogMessage(message, "GSPro", type, ConsoleColor.Green);

  }
}