using RedisProxy.Backend.Services;

namespace TestProject1;

public class RedisInfoParserTests
{
    [Fact]
    public void Parse_CorrectlyMaps_StandardRedisInfo()
    {
        string rawInfo = @"
# Server
redis_version:7.0.5
redis_mode:standalone

# Clients
connected_clients:5
blocked_clients:0

# Memory
used_memory:1048576
used_memory_human:1.00M
used_memory_rss:2097152
mem_fragmentation_ratio:2.00
maxmemory:0

# Stats
total_connections_received:100
total_commands_processed:5000
instantaneous_ops_per_sec:125
instantaneous_input_kbps:0.55
instantaneous_output_kbps:1.20
rejected_connections:0
keyspace_hits:450
keyspace_misses:50
evicted_keys:0
expired_keys:10

# Replication
role:master
master_link_status:up
master_repl_offset:0

# CPU
used_cpu_sys:10.5
used_cpu_user:5.2
used_cpu_sys_children:0.0
used_cpu_user_children:0.0
";

        var result = RedisInfoParser.Parse(rawInfo);

        Assert.NotNull(result);
        Assert.Equal(5, result.ConnectedClients);
        
        Assert.Equal(1048576, result.UsedMemory);
        Assert.Equal(2.0, result.FragmentationRatio);
        
        Assert.Equal(5000, result.TotalCommandsProcessed);
        Assert.Equal(125, result.OpsPerSec);
        Assert.Equal(0.55, result.InputKbps);
        Assert.Equal(450, result.KeyspaceHits);
        
        Assert.Equal(10.5, result.UsedCpuSys);
    }

    [Fact]
    public void Parse_Handles_EmptyOrMalformedInput()
    {
        string garbage = "This is not redis info\r\nHello:World";

        var result = RedisInfoParser.Parse(garbage);

        Assert.NotNull(result);
        Assert.Equal(0, result.UsedMemory); // Should default to 0
        Assert.Equal(0, result.ConnectedClients);
    }

    [Fact]
    public void Parse_Handles_MissingValues()
    {
        string partial = "used_memory:500\r\n";

        var result = RedisInfoParser.Parse(partial);

        Assert.Equal(500, result.UsedMemory);
        Assert.Equal(0, result.ConnectedClients); // Missing field is 0
    }
}