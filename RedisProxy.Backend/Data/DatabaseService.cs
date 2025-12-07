using RedisProxy.Backend.Metric;
using Npgsql;
using Dapper;

namespace RedisProxy.Backend.Data;

public class DatabaseService
{
    private readonly string _connectionString;

    public DatabaseService(IConfiguration config)
    {
        _connectionString = config.GetConnectionString("DefaultConnection")
                            ?? throw new Exception("Database connection string is missing");
    }

    public async Task InitializeDatabaseAsync()
    {
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        // 1. Table for Command Counts (Proxy Data)
        var requestLog = @"
                CREATE TABLE IF NOT EXISTS request_logs (
                    id SERIAL PRIMARY KEY,
                    timestamp TIMESTAMP NOT NULL,
                    command TEXT,
                    key_name TEXT,
                    latency_ms DOUBLE PRECISION,
                    is_success BOOLEAN,
                    is_hit BOOLEAN,
                    payload_size INT
                );
                
                -- Create an index on timestamp so graphs load fast
                CREATE INDEX IF NOT EXISTS idx_logs_time ON request_logs(timestamp);
            ";

        // 2. Table for Server Health (INFO Data)
        var infoLogs = @"
                CREATE TABLE IF NOT EXISTS server_metrics (
                    id SERIAL PRIMARY KEY,
                    timestamp TIMESTAMP NOT NULL,
                    
                    -- NEW CPU COLUMNS
                    used_cpu_sys DOUBLE PRECISION,
                    used_cpu_user DOUBLE PRECISION,
                    used_cpu_sys_children DOUBLE PRECISION,
                    used_cpu_user_children DOUBLE PRECISION,

                    input_kbps DOUBLE PRECISION,
                    output_kbps DOUBLE PRECISION,
                    connected_clients INT,
                    blocked_clients INT,
                    ops_per_sec BIGINT,
                    total_commands BIGINT,
                    keyspace_hits BIGINT,
                    keyspace_misses BIGINT,
                    used_memory BIGINT,
                    used_memory_rss BIGINT,
                    fragmentation_ratio DOUBLE PRECISION,
                    max_memory BIGINT,
                    evicted_keys BIGINT,
                    expired_keys BIGINT,
                    master_status TEXT,
                    repl_offset BIGINT
                );";

        await connection.ExecuteAsync(requestLog);
        await connection.ExecuteAsync(infoLogs);
    }


    public async Task SaveServerMetricsAsync(ServerMetricLog metric)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        var sql = @"
                INSERT INTO server_metrics (
                    timestamp, 
                    used_cpu_sys, used_cpu_user, used_cpu_sys_children, used_cpu_user_children, 
                    input_kbps, output_kbps, connected_clients, blocked_clients,
                    ops_per_sec, total_commands, keyspace_hits, keyspace_misses,
                    used_memory, used_memory_rss, fragmentation_ratio, max_memory,
                    evicted_keys, expired_keys, master_status, repl_offset
                ) VALUES (
                    @Timestamp, 
                    @UsedCpuSys, @UsedCpuUser, @UsedCpuSysChildren, @UsedCpuUserChildren,
                    @InputKbps, @OutputKbps, @ConnectedClients, @BlockedClients,
                    @OpsPerSec, @TotalCommandsProcessed, @KeyspaceHits, @KeyspaceMisses,
                    @UsedMemory, @UsedMemoryRss, @FragmentationRatio, @MaxMemory,
                    @EvictedKeys, @ExpiredKeys, @MasterLinkStatus, @MasterReplOffset
                )";
        await connection.ExecuteAsync(sql, metric);
    }

    public async Task SaveRequestLogsAsync(IEnumerable<RequestLog> logs)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        var sql = @"
        INSERT INTO request_logs (timestamp, command, key_name, latency_ms, is_success, is_hit, payload_size) 
        VALUES (@Timestamp, @Command, @Key, @LatencyMs, @IsSuccess, @IsHit, @PayloadSize)";

        // Dapper executes this as a batch automatically
        await connection.ExecuteAsync(sql, logs);
    }
}