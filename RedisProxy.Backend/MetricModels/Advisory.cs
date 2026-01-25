namespace RedisProxy.Backend.MetricModels;

public class Advisory
{
    public string Level { get; set; } = string.Empty; // "Critical", "Warning", "Info"
    public string Message { get; set; } = string.Empty;
    public string Command { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}