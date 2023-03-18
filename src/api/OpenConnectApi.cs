using System.Text.Json.Serialization;

namespace gspro_r10.OpenConnect
{
  public class OpenConnectApiMessage
  {
    public string DeviceID { get { return "GSPRO-R10"; } }
    public string Units { get { return "Yards"; } }
    public int ShotNumber { get; set; }
    public string APIVersion { get { return "1"; } }
    public BallData? BallData { get; set; }
    public ClubData? ClubData { get; set; }
    public ShotDataOptions? ShotDataOptions { get; set; }

    public static OpenConnectApiMessage CreateHeartbeat(bool launchMonitorReady = false)
    {
      return new OpenConnectApiMessage()
      {
        ShotNumber = 0,
        ShotDataOptions = new ShotDataOptions()
        {
          ContainsBallData = false,
          ContainsClubData = false,
          LaunchMonitorIsReady = launchMonitorReady,
          LaunchMonitorBallDetected = launchMonitorReady,
          IsHeartBeat = true
        }
      };
    }

    public static OpenConnectApiMessage CreateShotData(int shotNumber, BallData? ballData, ClubData? clubData = null)
    {
      return new OpenConnectApiMessage()
      {
        ShotNumber = shotNumber,
        BallData = ballData,
        ClubData = clubData,
        ShotDataOptions = new ShotDataOptions()
        {
          ContainsBallData = (ballData != null),
          ContainsClubData = (clubData != null),
        }
      };
    }


    public static OpenConnectApiMessage TestShot()
    {
      return new OpenConnectApiMessage()
      {
        ShotNumber = 0,
        BallData = new BallData()
        {
          Speed = 200,
          SpinAxis = -90,
          TotalSpin = 50000,
          SideSpin = 50000,
          BackSpin = -100000,
          HLA = 10,
          VLA = 20
        },
        ShotDataOptions = new ShotDataOptions()
        {
          ContainsBallData = true,
          ContainsClubData = false,
        }
      };
    }
  }

  public class ShotDataOptions
  {
    public bool ContainsBallData { get; set; }
    public bool ContainsClubData { get; set; }
    public bool? LaunchMonitorIsReady { get; set; }
    public bool? LaunchMonitorBallDetected { get; set; }
    public bool? IsHeartBeat { get; set; }
  }

  public class BallData
  {
    public double Speed { get; set; }
    public double SpinAxis { get; set; }
    public double TotalSpin { get; set; }
    public double BackSpin { get; set; }
    public double SideSpin { get; set; }
    public double HLA { get; set; }
    public double VLA { get; set; }
    public double CarryDistance { get; set; }

  }

  public class ClubData
  {
    public double Speed { get; set; }
    public double AngleOfAttack { get; set; }
    public double FaceToTarget { get; set; }
    public double Lie { get; set; }
    public double Loft { get; set; }
    public double Path { get; set; }
    public double SpeedAtImpact { get; set; }
    public double VerticalFaceImpact { get; set; }
    public double HorizontalFaceImpact { get; set; }
    public double ClosureRate { get; set; }

  }

  public class OpenConnectApiResponse
  {
    public int Code { get; set; }
    public string? Message { get; set; }
    public PlayerInfo? Player { get; set; }
  }

  public class PlayerInfo
  {
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public Handed? Handed { get; set; }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public Club? Club { get; set; }
    public float? DistanceToTarget { get; set; }
  }

  public enum Handed
  {
    RH,
    LH
  }

  public enum Club
  {
    unknown,
    DR,
    W2,
    W3,
    W4,
    W5,
    W6,
    W7,
    I1,
    I2,
    I3,
    I4,
    I5,
    I6,
    I7,
    I8,
    I9,
    H2,
    H3,
    H4,
    H5,
    H6,
    H7,
    PW,
    GW,
    SW,
    LW,
    PT
  }

}