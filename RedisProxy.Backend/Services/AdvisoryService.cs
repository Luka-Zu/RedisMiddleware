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

    public IEnumerable<Advisory> AnalyzeLog(RequestLog log, string rawContent)
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
        
        // NEW 4: Using SELECT
        if (log.Command.ToUpper() == "SELECT" && log.Key != "0") 
        {
            advisories.Add(new Advisory
            {
                Level = "Warning",
                Command = "SELECT",
                Message = "Anti-Pattern: Using multiple logical databases (SELECT) is deprecated in Cluster mode. Use separate Redis instances."
            });
        }
        
        // Rule 5: setting without ttl
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
        
        // RULE 6: JSON Blob in String
        if (log.Command.ToUpper() == "SET")
        {
            // Heuristic: Looks like JSON object "{" ... "}"
            // We verify it starts/ends with braces and contains at least one colon
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