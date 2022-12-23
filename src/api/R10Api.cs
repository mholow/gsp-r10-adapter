using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace gspro_r10.R10
{

  [JsonConverter(typeof(JsonStringEnumConverter))]
  public enum R10MessageType
  {
    Unknown,
    Handshake,
    Challenge,
    Authentication,
    SimCommand,
    Ping,
    Pong,
    SetBallData,
    Disconnect,
    SetClubData,
    SendShot,
    ShotComplete,
    Arm,
    Disarm
  }

  public class R10Message
  {
    public R10MessageType Type { get; set; }
  }

  public class SetBallDataMessage: R10Message
  {
    public BallData? BallData { get; set; }
  }

  public class BallData
  {
    public double TotalSpin { get; set; }
    public double LaunchAngle { get; set; }
    public double SpinAxis { get; set; }
    public double LaunchDirection { get; set; }
    public double BallSpeed { get; set; }
    public double? BackSpin { get; set; }
    public double? SideSpin { get; set; }
  }

  public class SetClubDataMessage : R10Message
  {
    public ClubData? ClubData { get; set; }
  }

  public class ClubData
  {
    public double ClubHeadSpeed { get; set; }
    public double ClubAnglePath { get; set; }
    public double ClubAngleFace { get; set; }
  }


  public class Response {  }

  public class HandshakeResponse : Response
  {
    // None of this actually matters, since we're always going to accept the challenge
    public R10MessageType Type { get { return R10MessageType.Handshake; } }
    public string Challenge { get { return string.Empty; } }
    public string E6Version { get { return string.Empty; } }
    public string ProtocolVersion { get { return string.Empty; } }
    public string RequiredProtocolVersion { get { return string.Empty; } }

  }

  public class ChallengeResponse : Response
  {
    public R10MessageType Type { get { return R10MessageType.Authentication; } }
    public string Success { get { return true.ToString().ToLower(); } }
  }

  public class SuccessResponse: Response
  {
      public string Details { get; set; }
      public R10MessageType SubType { get; set; }
      public string Type { get { return "ACK"; } }

      public SuccessResponse(R10MessageType type, string details = "Success.")
      {
        SubType = type;
        Details = details;
      }
  }

  

  public class ShotCompleteDetails
  {
    public double Apex { get; set; }
    public BallData? BallData { get; set; }
    public bool BallInHole { get; set; }
    public string? BallLocation { get; set; }
    public double CarryDeviationAngle { get; set; }
    public double TotalDeviationAngle { get; set; }
    public double CarryDeviationFeet { get; set; }
    public double TotalDeviationFeet { get; set; }
    public double CarryDistance { get; set; }
    public double TotalDistance { get; set; }
    public ClubData? ClubData { get; set; }
    public double DistanceToPin { get; set; }

    public static ShotCompleteDetails Empty()
    {
      return new ShotCompleteDetails()
      {
        Apex = 0,
        BallData = new BallData(),
        BallInHole = false,
        BallLocation = "",
        CarryDeviationAngle = 0,
        CarryDeviationFeet = 0,
        CarryDistance = 0,
        TotalDeviationAngle = 0,
        TotalDeviationFeet = 0,
        TotalDistance = 0,
        ClubData = new ClubData(),
        DistanceToPin = 0
      };
    }

  }

  public class ShotCompleteMessage: Response
  {
    public ShotCompleteDetails? Details { get; set; }
    public R10MessageType Type { get { return R10MessageType.SimCommand; } }
    public R10MessageType SubType { get { return R10MessageType.ShotComplete; } }
  }

  public class ArmMessage: Response
  {
    public R10MessageType SubType { get { return R10MessageType.Arm; } }
    public R10MessageType Type { get { return R10MessageType.SimCommand; } }
  }

  public class DisrmMessage: Response
  {
    public R10MessageType SubType { get { return R10MessageType.Disarm; } }
    public R10MessageType Type { get { return R10MessageType.SimCommand; } }
  }

  public class PingMessage : Response
  {
    public R10MessageType SubType { get { return R10MessageType.Ping; } }
    public R10MessageType Type { get { return R10MessageType.SimCommand; } }
  }
}