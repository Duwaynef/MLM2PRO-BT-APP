using System.Text.Json.Serialization;

namespace MLM2PRO_BT_APP.api;

public class PuttingDataMessage
{
    [JsonPropertyName("ballData")]
    public BallData? BallData { get; init; }
}

public abstract class BallData(double ballSpeed, double totalSpin, double launchDirection)
{
    [JsonPropertyName("BallSpeed")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)] // Ensure this attribute is here
    public double BallSpeed { get; } = ballSpeed;

    [JsonPropertyName("TotalSpin")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public double TotalSpin { get; } = totalSpin;

    [JsonPropertyName("LaunchDirection")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public double LaunchDirection { get; } = launchDirection;
}
