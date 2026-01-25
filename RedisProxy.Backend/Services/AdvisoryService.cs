using RedisProxy.Backend.Metric;
using RedisProxy.Backend.MetricModels;

namespace RedisProxy.Backend.Services;

public class AdvisoryService : IAdvisoryService
{
    // --- THRESHOLDS ---
    private const int LargePayloadThreshold = 50000; // 50KB
    private const double HighLatencyThresholdMs = 50.0; // 50ms warning

    // --- RULES: Blocking Commands ---
    private static readonly HashSet<string> BlockingCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "KEYS", "FLUSHALL", "FLUSHDB", "SAVE"
    };

    public IEnumerable<Advisory> AnalyzeLog(RequestLog log)
    {
        var advisories = new List<Advisory>();

        // Rule 1: The Thread Blocker (Critical)
        if (BlockingCommands.Contains(log.Command))
        {
            advisories.Add(new Advisory
            {
                Level = "Critical",
                Command = log.Command,
                Message = $"Avoid using {log.Command} in production. It blocks the single Redis thread. Use SCAN or async alternatives."
            });
        }

        // Rule 2: The Network Hog (Warning)
        if (log.PayloadSize > LargePayloadThreshold)
        {
            advisories.Add(new Advisory
            {
                Level = "Warning",
                Command = log.Command,
                Message = $"Large payload detected ({log.PayloadSize / 1024} KB). Big values saturate network bandwidth. Consider compression or splitting data."
            });
        }

        // Rule 3: Latency Spike (Warning)
        if (log.LatencyMs > HighLatencyThresholdMs)
        {
            advisories.Add(new Advisory
            {
                Level = "Warning",
                Command = log.Command,
                Message = $"Slow query detected ({log.LatencyMs}ms). Check command complexity (O(N)) or network latency."
            });
        }

        return advisories;
    }
}


public interface IAdvisoryService
{
    IEnumerable<Advisory> AnalyzeLog(RequestLog log);
}