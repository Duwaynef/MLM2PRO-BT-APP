using System.Text.Json.Serialization;

namespace MLM2PRO_BT_APP.api;

public class PuttingDataMessage
{
    [JsonPropertyName("ballData")]
    public BallData? BallData { get; init; }
}
public class BallData
{
    [JsonPropertyName("BallSpeed")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public double BallSpeed { get; set; }

    [JsonPropertyName("TotalSpin")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public double TotalSpin { get; set; }

    [JsonPropertyName("LaunchDirection")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public double LaunchDirection { get; set; }
}
