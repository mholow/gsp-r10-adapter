using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using gspro_r10.R10;
using Microsoft.Extensions.Configuration;
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
      R10Logger.LogR10Info($"TCP session with Id {Id} connected!");
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
      R10Logger.LogR10Outgoing(message);
      return base.SendAsync(message);
    }

    protected override void OnDisconnected()
    {
      R10Logger.LogR10Info($"TCP session with Id {Id} disconnected!");
      PingTimer?.Dispose();
    }

    protected override void OnReceived(byte[] buffer, long offset, long size)
    {
      string message = Encoding.UTF8.GetString(buffer, (int)offset, (int)size);
      R10Logger.LogR10Incoming(message);

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
          ConnectionManager.SendShot(BallDataFromR10BallData(BallData), ClubDataFromR10ClubData(ClubData));
          CompleteShot();
          response = new SuccessResponse(R10MessageType.SendShot);
          break;
        case R10MessageType.Disconnect:
          Disconnect();
          break;
        default:
          response = new SuccessResponse(m.Type);
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
      SendAsync(JsonSerializer.Serialize(new DisrmMessage()));
      Thread.Sleep(500);
      SendAsync(JsonSerializer.Serialize(new ShotCompleteMessage()
      {
        Details = new ShotCompleteDetails()
        {
          BallData = this.BallData,
          ClubData = this.ClubData,
          Apex = 0,
          DistanceToPin = 0,
          CarryDeviationAngle = 0,
          CarryDeviationFeet = 0,
          CarryDistance = 0,
          TotalDeviationAngle = 0,
          TotalDeviationFeet = 0,
          TotalDistance = 0,
          BallInHole = false,
          BallLocation = "Fairway"
        }
      }));
      ClubData = null;
      BallData = null;
      Thread.Sleep(500);
      SendAsync(JsonSerializer.Serialize(new ArmMessage()));
    }

    protected override void OnError(SocketError error)
    {
      R10Logger.LogR10Error($"TCP session caught an error with code {error}");
    }

    public static OpenConnect.BallData? BallDataFromR10BallData(R10.BallData? r10BallData)
    {
      if (r10BallData == null) return null;
      return new OpenConnect.BallData()
      {
        Speed = r10BallData.BallSpeed,
        SpinAxis = -1 * (r10BallData.SpinAxis < 90 ? r10BallData.SpinAxis : r10BallData.SpinAxis - 360),
        TotalSpin = r10BallData.TotalSpin,
        HLA = r10BallData.LaunchDirection,
        VLA = r10BallData.LaunchAngle,
        SideSpin = r10BallData.TotalSpin * -1 * Math.Sin(r10BallData.SpinAxis * Math.PI / 180),
        BackSpin = r10BallData.TotalSpin * Math.Cos(r10BallData.SpinAxis * Math.PI / 180)
      };
    }

    public static OpenConnect.ClubData? ClubDataFromR10ClubData(R10.ClubData? r10ClubData)
    {
      if (r10ClubData == null) return null;
      return new OpenConnect.ClubData()
      {
        Speed = r10ClubData.ClubHeadSpeed,
        SpeedAtImpact = r10ClubData.ClubHeadSpeed,
        Path = r10ClubData.ClubAnglePath,
        FaceToTarget = r10ClubData.ClubAngleFace
      };
    }
  }

  class R10ConnectionServer : TcpServer
  {
    ConnectionManager ConnectionManager;
    public R10ConnectionServer(ConnectionManager connectionManager, IConfigurationSection configuration)
      : base(IPAddress.Any, int.Parse(configuration["port"] ?? "2483"))
    {
      ConnectionManager = connectionManager;
    }

    public override bool Start()
    {
      R10Logger.LogR10Info($"Server starting at IP: {GetLocalIPAddress()} Port: {Port}...");
      this.OptionKeepAlive = true;
      this.OptionTcpKeepAliveRetryCount = 3;
      this.OptionTcpKeepAliveInterval = 60;
      this.OptionTcpKeepAliveTime = 60;
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
      R10Logger.LogR10Error($"TCP server caught an error with code {error}");
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

  public static class R10Logger
  {
    public static void LogR10Info(string message) => LogR10Message(message, LogMessageType.Informational);
    public static void LogR10Error(string message) => LogR10Message(message, LogMessageType.Error);
    public static void LogR10Outgoing(string message) => LogR10Message(message, LogMessageType.Outgoing);
    public static void LogR10Incoming(string message) => LogR10Message(message, LogMessageType.Incoming);
    public static void LogR10Message(string message, LogMessageType type) => BaseLogger.LogMessage(message, "R10-E6", type, ConsoleColor.Blue);
  }
}