using RedisProxy.Backend.MetricModels;

namespace RedisProxy.Backend.Services;

public static class RedisInfoParser
{
    public static ServerMetricLog Parse(string info)
    {
        var dict = new Dictionary<string, string>();
        
        if (string.IsNullOrWhiteSpace(info)) 
            return new ServerMetricLog { Timestamp = DateTime.UtcNow };

        var lines = info.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            if (line.StartsWith("#") || !line.Contains(":")) continue;
            var parts = line.Split(':', 2);
            if (parts.Length == 2)
            {
                dict[parts[0].Trim()] = parts[1].Trim();
            }
        }

        long GetLong(string key) => dict.TryGetValue(key, out var v) && long.TryParse(v, out var l) ? l : 0;
        double GetDouble(string key) => dict.TryGetValue(key, out var v) && double.TryParse(v, out var d) ? d : 0;
        string GetString(string key) => dict.TryGetValue(key, out var v) ? v : "unknown";

        return new ServerMetricLog
        {
            Timestamp = DateTime.UtcNow,
            
            InputKbps = GetDouble("instantaneous_input_kbps"),
            OutputKbps = GetDouble("instantaneous_output_kbps"),
            ConnectedClients = (int)GetLong("connected_clients"),
            BlockedClients = (int)GetLong("blocked_clients"),

            OpsPerSec = GetLong("instantaneous_ops_per_sec"),
            TotalCommandsProcessed = GetLong("total_commands_processed"),
            KeyspaceHits = GetLong("keyspace_hits"),
            KeyspaceMisses = GetLong("keyspace_misses"),

            UsedMemory = GetLong("used_memory"),
            UsedMemoryRss = GetLong("used_memory_rss"),
            FragmentationRatio = GetDouble("mem_fragmentation_ratio"),
            MaxMemory = GetLong("maxmemory"),

            EvictedKeys = GetLong("evicted_keys"),
            ExpiredKeys = GetLong("expired_keys"),

            MasterLinkStatus = GetString("master_link_status"),
            MasterReplOffset = GetLong("master_repl_offset"),
                
            UsedCpuSys = GetDouble("used_cpu_sys"),
            UsedCpuUser = GetDouble("used_cpu_user"),
            UsedCpuSysChildren = GetDouble("used_cpu_sys_children"),
            UsedCpuUserChildren = GetDouble("used_cpu_user_children"),
        };
    }
}