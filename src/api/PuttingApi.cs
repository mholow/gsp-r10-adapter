using System.Text.Json.Serialization;

namespace gspro_r10.Putting
{
  public class PuttingDataMessage
  {
    public BallData? ballData { get; set; }
  }

  [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
  public class BallData
  {
    public float BallSpeed {get; set; }
    public float TotalSpin { get; set; }
    public float LaunchDirection { get; set; }
  }
}