namespace RedisProxy.Backend.Metric;

public class CommandMetricLog
{
    public DateTime Timestamp { get; set; }
    public string Command { get; set; } = string.Empty; // e.g., "SET", "GET"
    public int Count { get; set; }
}