using System.Text.Json.Serialization;

namespace MLM2PRO_BT_APP.Putting;

public class PuttingDataMessage
{
    [JsonPropertyName("ballData")]
    public BallData? BallData { get; set; }
}

public class BallData
{
    [JsonPropertyName("BallSpeed")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)] // Ensure this attribute is here
    public double BallSpeed { get; set; }

    [JsonPropertyName("TotalSpin")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public double TotalSpin { get; set; }

    [JsonPropertyName("LaunchDirection")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public double LaunchDirection { get; set; }
}
