using System.Text.Json.Serialization;

namespace MLM2PRO_BT_APP;

public class PuttingDataMessage
{
public PuttingBallData? PuttingBallData { get; set; }
}

[JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
public class PuttingBallData
{
public float BallSpeed {get; set; }
public float TotalSpin { get; set; }
public float LaunchDirection { get; set; }
}