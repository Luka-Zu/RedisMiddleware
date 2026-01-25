using RedisProxy.Backend.Metric;
using Npgsql;
using Dapper;
using RedisProxy.Backend.MetricModels;

namespace RedisProxy.Backend.Data;

public class DatabaseService(IConfiguration config)
{
    private readonly string _connectionString = config.GetConnectionString("DefaultConnection")
                                                ?? throw new Exception("Database connection string is missing");

    public async Task InitializeDatabaseAsync()
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

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
        await using var connection = new NpgsqlConnection(_connectionString);
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
        await using var connection = new NpgsqlConnection(_connectionString);
        var sql = @"
        INSERT INTO request_logs (timestamp, command, key_name, latency_ms, is_success, is_hit, payload_size) 
        VALUES (@Timestamp, @Command, @Key, @LatencyMs, @IsSuccess, @IsHit, @PayloadSize)";

        await connection.ExecuteAsync(sql, logs);
    }
    
    public async Task<IEnumerable<CommandStat>> GetCommandStatsAsync(DateTime since)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        var sql = @"
        SELECT 
            command as Command, 
            COUNT(*) as Count 
        FROM request_logs 
        WHERE timestamp >= @Since 
        GROUP BY command 
        ORDER BY count DESC 
        LIMIT 5"; // Gets Top 5 commands

        return await connection.QueryAsync<CommandStat>(sql, new { Since = since });
    }
    
    public async Task<IEnumerable<ServerMetricLog>> GetServerMetricsSinceAsync(DateTime since)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        var sql = @"
        SELECT 
            timestamp, 
            -- CPU
            used_cpu_sys as UsedCpuSys, 
            used_cpu_user as UsedCpuUser, 
            
            -- Memory Analysis
            used_memory as UsedMemory, 
            used_memory_rss as UsedMemoryRss,
            fragmentation_ratio as FragmentationRatio,
            evicted_keys as EvictedKeys,

            -- Network I/O
            input_kbps as InputKbps,
            output_kbps as OutputKbps,

            -- Cache Efficiency
            keyspace_hits as KeyspaceHits,
            keyspace_misses as KeyspaceMisses,

            -- General Load
            connected_clients as ConnectedClients,
            ops_per_sec as OpsPerSec
        FROM server_metrics 
        WHERE timestamp >= @Since
        ORDER BY timestamp ASC
        LIMIT 30000";         

        return await connection.QueryAsync<ServerMetricLog>(sql, new { Since = since });
    }

    public async Task<IEnumerable<RequestLog>> GetRequestLogsSinceAsync(DateTime since)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        var sql = @"
            SELECT 
                timestamp, 
                command, 
                key_name as Key, 
                latency_ms as LatencyMs, 
                is_success as IsSuccess, 
                is_hit as IsHit, 
                payload_size as PayloadSize
            FROM request_logs 
            WHERE timestamp >= @Since
            ORDER BY timestamp DESC
            LIMIT 5000";

        return await connection.QueryAsync<RequestLog>(sql, new { Since = since });
    }
    
    public async Task<IEnumerable<HotKeyStat>> GetHotKeysAsync(DateTime since)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        var sql = @"
        SELECT 
            key_name as Key, 
            COUNT(*) as Count 
        FROM request_logs 
        WHERE timestamp >= @Since 
          AND key_name IS NOT NULL 
          AND key_name != ''
        GROUP BY key_name 
        ORDER BY count DESC 
        LIMIT 10"; // Get Top 10

        return await connection.QueryAsync<HotKeyStat>(sql, new { Since = since });
    }
    
    public async Task<IEnumerable<string>> GetKeysSinceAsync(DateTime since)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        var sql = @"
            SELECT key_name 
            FROM request_logs 
            WHERE timestamp >= @Since 
              AND key_name IS NOT NULL 
              AND key_name != ''
            -- Limit to prevent exploding CPU if you have millions of logs
            LIMIT 100000"; 

        return await connection.QueryAsync<string>(sql, new { Since = since });
    }
    
    public async Task<IEnumerable<RequestLog>> GetRequestLogsRangeAsync(DateTime from, DateTime to)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        var sql = @"
            SELECT * FROM request_logs 
            WHERE timestamp BETWEEN @From AND @To
            ORDER BY timestamp ASC"; // Critical: Must be in order!

        return await connection.QueryAsync<RequestLog>(sql, new { From = from, To = to });
    }
    
}