namespace RedisProxy.Backend.Workers;
using System.Net.Sockets;
using System.Text;
using RedisProxy.Backend.Data;
using RedisProxy.Backend.Metric;


public class RedisMonitorWorker(ILogger<RedisMonitorWorker> logger, DatabaseService db) : BackgroundService
{
    private const string RemoteHost = "127.0.0.1";
    private const int RemotePort = 6379;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait for DB init
        await Task.Delay(2000, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var stats = await FetchRedisInfoAsync();
                if (stats != null)
                {
                    await db.SaveServerMetricsAsync(stats);
                    logger.LogInformation("Collected and saved Redis Server Metrics (INFO).");
                }
            }
            catch (Exception ex)
            {
                logger.LogError($"Failed to collect Redis stats: {ex.Message}");
            }

            // Poll every 5 seconds
            await Task.Delay(5000, stoppingToken);
        }
    }

    private async Task<ServerMetricLog?> FetchRedisInfoAsync()
    {
        using var client = new TcpClient();
        await client.ConnectAsync(RemoteHost, RemotePort);
        using var stream = client.GetStream();

        // 1. Send INFO command
        var cmdBytes = Encoding.UTF8.GetBytes("INFO\r\n");
        await stream.WriteAsync(cmdBytes);

        // 2. Read Response (Simplified reading for brevity - in production use a loop)
        var buffer = new byte[65536]; 
        var bytesRead = await stream.ReadAsync(buffer);
        var response = Encoding.UTF8.GetString(buffer, 0, bytesRead);

        return ParseInfoResponse(response);
    }

    private ServerMetricLog ParseInfoResponse(string info)
    {
        var dict = new Dictionary<string, string>();
        var lines = info.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            if (line.StartsWith("#") || !line.Contains(":")) continue;
            var parts = line.Split(':', 2);
            dict[parts[0]] = parts[1];
        }

        // Helper to safely parse
        long GetLong(string key) => dict.TryGetValue(key, out var v) && long.TryParse(v, out var l) ? l : 0;
        double GetDouble(string key) => dict.TryGetValue(key, out var v) && double.TryParse(v, out var d) ? d : 0;
        string GetString(string key) => dict.TryGetValue(key, out var v) ? v : "unknown";

        return new ServerMetricLog
        {
            Timestamp = DateTime.UtcNow,
            
            // Traffic
            InputKbps = GetDouble("instantaneous_input_kbps"),
            OutputKbps = GetDouble("instantaneous_output_kbps"),
            ConnectedClients = (int)GetLong("connected_clients"),
            BlockedClients = (int)GetLong("blocked_clients"),

            // Activity
            OpsPerSec = GetLong("instantaneous_ops_per_sec"),
            TotalCommandsProcessed = GetLong("total_commands_processed"),
            KeyspaceHits = GetLong("keyspace_hits"),
            KeyspaceMisses = GetLong("keyspace_misses"),

            // Memory
            UsedMemory = GetLong("used_memory"),
            UsedMemoryRss = GetLong("used_memory_rss"),
            FragmentationRatio = GetDouble("mem_fragmentation_ratio"),
            MaxMemory = GetLong("maxmemory"),

            // Key Health
            EvictedKeys = GetLong("evicted_keys"),
            ExpiredKeys = GetLong("expired_keys"),

            // Replication
            MasterLinkStatus = GetString("master_link_status"),
            MasterReplOffset = GetLong("master_repl_offset"),
                
            // CPU    
            UsedCpuSys = GetDouble("used_cpu_sys"),
            UsedCpuUser = GetDouble("used_cpu_user"),
            UsedCpuSysChildren = GetDouble("used_cpu_sys_children"),
            UsedCpuUserChildren = GetDouble("used_cpu_user_children"),
        };
    }
}