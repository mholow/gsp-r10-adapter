using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using gspro_r10.R10;
using NetCoreServer;

namespace gspro_r10
{
  class R10Session : TcpSession
  {
    public bool ReceivedPong { get; private set; }
    public Timer? PingTimer { get; private set; }
    public BallData? BallData { get; private set; }
    public ClubData? ClubData { get; private set; }

    public ConnectionManager ConnectionManager;

    public R10Session(TcpServer server, ConnectionManager connectionManager) : base(server)
    {
      this.ConnectionManager = connectionManager;
    }

    protected override void OnConnected()
    {
      SimpleLogger.LogR10Info($"TCP session with Id {Id} connected!");
      PingTimer = new Timer(SendPing, null, 0, 10000);
    }

    private void SendPing(object? state)
    {
      if (ReceivedPong)
      {
        ReceivedPong = false;
        string responseJson = JsonSerializer.Serialize(new PingMessage());
        SendAsync(responseJson);
      }
      else
      {
        //Console.WriteLine("Pong not receivied?");
      }
    }

    public override bool SendAsync(string message)
    {
      SimpleLogger.LogR10Outgoing(message);
      return base.SendAsync(message);
    }



    protected override void OnDisconnected()
    {
      SimpleLogger.LogR10Info($"TCP session with Id {Id} disconnected!");
      PingTimer?.Dispose();
    }

    protected override void OnReceived(byte[] buffer, long offset, long size)
    {
      string message = Encoding.UTF8.GetString(buffer, (int)offset, (int)size);
      SimpleLogger.LogR10Incoming(message);

      R10Message m;
      try
      {
        m = JsonSerializer.Deserialize<R10Message>(message) ?? new R10Message();
      }
      catch (JsonException)
      {
        m = new R10Message();
      }

      object? response = null;
      switch (m.Type)
      {
        case R10MessageType.Handshake:
          response = new HandshakeResponse();
          break;
        case R10MessageType.Challenge:
          response = new ChallengeResponse();
          ReceivedPong = true;
          break;
        case R10MessageType.Pong:
          ReceivedPong = true;
          break;
        case R10MessageType.SetBallData:
          SetBallDataMessage? setBallDataMessage = JsonSerializer.Deserialize<SetBallDataMessage>(message);
          BallData = setBallDataMessage?.BallData;
          response = new SuccessResponse(R10MessageType.SetBallData);
          break;
        case R10MessageType.SetClubData:
          SetClubDataMessage? setClubDataMessage = JsonSerializer.Deserialize<SetClubDataMessage>(message);
          ClubData = setClubDataMessage?.ClubData;
          response = new SuccessResponse(R10MessageType.SetClubData);
          break;
        case R10MessageType.SendShot:
          ConnectionManager.SendShot(this, BallData, ClubData);
          ClubData = null;
          BallData = null;
          break;
        case R10MessageType.Disconnect:
          Disconnect();
          break;
        default:
          break;
      };

      if (response != null)
      {
        string responseJson = JsonSerializer.Serialize(response);
        SendAsync(responseJson);
      }
    }

    public void CompleteShot()
    {
      SendAsync(JsonSerializer.Serialize(new ShotCompleteMessage()
      {
        Details = ShotCompleteDetails.Empty()
      }));
      Thread.Sleep(100);
      SendAsync(JsonSerializer.Serialize(new DisrmMessage()));
      Thread.Sleep(100);
      SendAsync(JsonSerializer.Serialize(new ArmMessage()));
    }

    protected override void OnError(SocketError error)
    {
      SimpleLogger.LogR10Error($"TCP session caught an error with code {error}");
    }


  }

  class R10ConnectionServer : TcpServer
  {
    ConnectionManager ConnectionManager;
    public R10ConnectionServer(ConnectionManager connectionManager, int port) : base(IPAddress.Any, port)
    {
      ConnectionManager = connectionManager;
    }

    public override bool Start()
    {
      SimpleLogger.LogR10Info($"Server starting at IP: {GetLocalIPAddress()} Port: {Port}...");
      return base.Start();
    }

    public void SendToAllSesions(string message)
    {
      foreach (var session in Sessions)
      {
        session.Value.SendAsync(message);
      }
    }


    protected override TcpSession CreateSession() { return new R10Session(this, ConnectionManager); }

    protected override void OnError(SocketError error)
    {
      
      SimpleLogger.LogR10Error($"TCP server caught an error with code {error}");
    }

    public static string GetLocalIPAddress()
    {
      var host = Dns.GetHostEntry(Dns.GetHostName());
      foreach (var ip in host.AddressList)
      {
        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
          return ip.ToString();
        }
      }
      throw new Exception("No network adapters with an IPv4 address in the system!");
    }
  }
}