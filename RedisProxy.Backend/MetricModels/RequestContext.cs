namespace RedisProxy.Backend.Metric;

using System.Diagnostics;

public class RequestContext
{
    public string Command { get; set; }
    public string Key { get; set; }
    public int PayloadSize { get; set; }
    
    // NEW: High-precision timer
    public long StartTimestamp { get; set; } 

    public RequestContext()
    {
        // Stopwatch.GetTimestamp() is more precise than DateTime.Now
        StartTimestamp = Stopwatch.GetTimestamp();
    }
}