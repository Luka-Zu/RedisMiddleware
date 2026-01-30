namespace RedisProxy.Backend.MetricModels;

public class RequestLog
{
    public DateTime Timestamp { get; set; }
    public string Command { get; set; }
    public string Key { get; set; }
    
    // Performance
    public double LatencyMs { get; set; }
    
    // Status
    public bool IsSuccess { get; set; }
    public bool IsHit { get; set; }
    public int PayloadSize { get; set; }
    
    public string? RawContent { get; set; }
}