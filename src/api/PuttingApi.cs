namespace gspro_r10.api;

public class PuttingMessage
{
    public PuttingBallData ballData { get; set; }
}

public class PuttingBallData
{
    public double BallSpeed { get; set; }
    public double TotalSpin { get; set; }
    public double LaunchDirection { get; set; }
}

public class PuttingResponse
{
    public bool result { get; set; }
}