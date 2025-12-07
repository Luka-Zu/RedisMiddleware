namespace RedisProxy.Backend.Metric;

public class RequestLog
{
    public DateTime Timestamp { get; set; }
    public string Command { get; set; }
    public string Key { get; set; }
    
    // Performance
    public double LatencyMs { get; set; }
    
    // Status
    public bool IsSuccess { get; set; } // True if Redis didn't return an Error (-)
    public bool IsHit { get; set; }     // True if GET found data
    public int PayloadSize { get; set; }
}