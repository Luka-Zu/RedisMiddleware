using System.Diagnostics;

namespace RedisProxy.Backend.MetricModels;

public class RequestContext
{
    public string Command { get; set; }
    public string Key { get; set; }
    public int PayloadSize { get; set; }
    
    public long StartTimestamp { get; set; } = Stopwatch.GetTimestamp();
    
    public string RawContent { get; set; } = "";
}