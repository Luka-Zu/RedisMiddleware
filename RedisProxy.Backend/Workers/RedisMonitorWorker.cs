using Microsoft.AspNetCore.SignalR;
using RedisProxy.Backend.Hubs;

namespace RedisProxy.Backend.Workers;
using System.Net.Sockets;
using System.Text;
using Data;
using Metric;


public class RedisMonitorWorker(ILogger<RedisMonitorWorker> logger, 
    DatabaseService db,
    IHubContext<MetricsHub, IMetricsClient> hub, IConfiguration config) : BackgroundService
{
    private readonly string _remoteHost = config["RedisSettings:Host"] ?? "127.0.0.1";
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
                    await hub.Clients.All.ReceiveServerUpdate(stats);
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
        await client.ConnectAsync(_remoteHost, RemotePort);
        using var stream = client.GetStream();

        var cmdBytes = Encoding.UTF8.GetBytes("INFO\r\n");
        await stream.WriteAsync(cmdBytes);

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