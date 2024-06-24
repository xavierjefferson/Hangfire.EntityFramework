using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Serilog.Events;

namespace Hangfire.EntityFrameworkStorage.SampleStuff;

public class LogItem
{
    public LogItem()
    {
    }

    public LogItem(LogEvent logEvent)
    {
        var properties = new Dictionary<string, string>();
        if (logEvent.Properties != null)
            foreach (var m in logEvent.Properties.Keys)
                properties[m] = logEvent.Properties[m].ToString();
        Id = Guid.NewGuid().ToString();
        Timestamp = logEvent.Timestamp.ToString("yyyy.MM.dd HH:mm:ss.fff");
        dt = logEvent.Timestamp.UtcDateTime;
        Level = logEvent.Level.ToString();
        Message = logEvent.RenderMessage();
        Exception = logEvent.Exception?.ToString() ?? "-";
        Properties = JsonSerializer.Serialize(properties);
    }

    [JsonPropertyName("dt")] public virtual DateTime dt { get; set; }
    [JsonPropertyName("id")] public virtual string Id { get; set; }

    [JsonPropertyName("timestamp")] public virtual string Timestamp { get; set; }

    [JsonPropertyName("level")] public virtual string Level { get; set; }

    [JsonPropertyName("exception")] public virtual string Exception { get; set; }

    [JsonPropertyName("message")] public virtual string Message { get; set; }

    [JsonPropertyName("properties")] public virtual string Properties { get; set; }
}