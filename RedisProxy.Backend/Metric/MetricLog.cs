namespace RedisProxy.Backend.Metric;

public class MetricLog
{
    public int Id { get; set; }
    public DateTime Timestamp { get; set; }
    public string Command { get; set; } = string.Empty;
    public string KeyPrefix { get; set; } = string.Empty;
    public int Count { get; set; }
}