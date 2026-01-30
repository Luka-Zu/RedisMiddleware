using RedisProxy.Backend.MetricModels;

namespace RedisProxy.Backend.Services;

public class AdvisoryService : IAdvisoryService
{
    private const int LargePayloadThreshold = 50000; // 50KB
    private const double HighLatencyThresholdMs = 50.0; // 50ms warning

    private static readonly HashSet<string> BlockingCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "KEYS", "FLUSHALL", "FLUSHDB", "SAVE"
    };

    public IEnumerable<Advisory> AnalyzeLog(RequestLog log, string rawContent)
    {
        var advisories = new List<Advisory>();

        if (BlockingCommands.Contains(log.Command))
        {
            advisories.Add(new Advisory
            {
                Level = "Critical",
                Command = log.Command,
                Message = $"Avoid using {log.Command} in production. It blocks the single Redis thread. Use SCAN or async alternatives."
            });
        }

        if (log.PayloadSize > LargePayloadThreshold)
        {
            advisories.Add(new Advisory
            {
                Level = "Warning",
                Command = log.Command,
                Message = $"Large payload detected ({log.PayloadSize / 1024} KB). Big values saturate network bandwidth. Consider compression or splitting data."
            });
        }

        if (log.LatencyMs > HighLatencyThresholdMs)
        {
            advisories.Add(new Advisory
            {
                Level = "Warning",
                Command = log.Command,
                Message = $"Slow query detected ({log.LatencyMs}ms). Check command complexity (O(N)) or network latency."
            });
        }
        
        if (log.Command.ToUpper() == "SELECT" && log.Key != "0") 
        {
            advisories.Add(new Advisory
            {
                Level = "Warning",
                Command = "SELECT",
                Message = "Anti-Pattern: Using multiple logical databases (SELECT) is deprecated in Cluster mode. Use separate Redis instances."
            });
        }
        
        if (log.Command.ToUpper() == "SET")
        {
            bool hasTTL = rawContent.Contains("EX", StringComparison.OrdinalIgnoreCase) || 
                          rawContent.Contains("PX", StringComparison.OrdinalIgnoreCase);

            if (!hasTTL)
            {
                advisories.Add(new Advisory
                {
                    Level = "Warning",
                    Command = "SET",
                    Message = $"Memory Risk: Key({log.Key}) cached without TTL (Expiration). It will never expire. Use 'SET ... EX 60'."
                });
            }
        }
        
        if (log.Command.ToUpper() == "SET")
        {
            var val = rawContent.Trim();
            if (val.Contains("{") && val.Contains("}") && val.Contains(":"))
            {
                advisories.Add(new Advisory
                {
                    Level = "Info",
                    Command = "JSON",
                    Message = "Data Modeling: Detected JSON blob. Consider using RedisJSON or Hash (HSET) for field-level updates."
                });
            }
        }

        return advisories;
    }
}


public interface IAdvisoryService
{
    IEnumerable<Advisory> AnalyzeLog(RequestLog log, string rawContent);
}