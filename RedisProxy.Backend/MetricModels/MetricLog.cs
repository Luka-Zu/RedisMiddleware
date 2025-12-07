namespace RedisProxy.Backend.Metric;

public class ServerMetricLog
{
    public DateTime Timestamp { get; set; }
    
    // Traffic
    public double InputKbps { get; set; }
    public double OutputKbps { get; set; }
    public int ConnectedClients { get; set; }
    public int BlockedClients { get; set; }

    // Activity
    public long OpsPerSec { get; set; }
    public long TotalCommandsProcessed { get; set; }
    public long KeyspaceHits { get; set; }
    public long KeyspaceMisses { get; set; }

    // Memory
    public long UsedMemory { get; set; }
    public long UsedMemoryRss { get; set; }
    public double FragmentationRatio { get; set; }
    public long MaxMemory { get; set; }

    // Key Health
    public long EvictedKeys { get; set; }
    public long ExpiredKeys { get; set; }

    // Replication
    public string MasterLinkStatus { get; set; } = "unknown";
    public long MasterReplOffset { get; set; }
    
    // CPU Metrics
    public double UsedCpuSys { get; set; }
    public double UsedCpuUser { get; set; }
    public double UsedCpuSysChildren { get; set; }
    public double UsedCpuUserChildren { get; set; }
}