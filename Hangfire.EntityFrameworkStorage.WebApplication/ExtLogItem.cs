 

using System.Text.Json.Serialization;

namespace Hangfire.EntityFrameworkStorage.WebApplication;

public class ExtLogItem
{
    [JsonPropertyName("id")] public string id { get; set; }

    [JsonPropertyName("timestamp")] public string timestamp { get; set; }

    [JsonPropertyName("level")] public string level { get; set; }

    [JsonPropertyName("message")] public string message { get; set; }

    [JsonPropertyName("exception")] public string exception { get; set; }

    [JsonPropertyName("properties")] public string properties { get; set; }
}