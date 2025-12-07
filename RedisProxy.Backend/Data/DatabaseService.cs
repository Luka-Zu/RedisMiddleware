using RedisProxy.Backend.Metric;
using Npgsql;
using Dapper;

namespace RedisProxy.Backend.Data;


public class DatabaseService
{
    private readonly string _connectionString;

    public DatabaseService(IConfiguration config)
    {
        // We will add this connection string to appsettings.json next
        _connectionString = config.GetConnectionString("DefaultConnection") 
                            ?? throw new Exception("Database connection string is missing");
    }

    // Run this once at startup
    public async Task InitializeDatabaseAsync()
    {
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        // Create table if it doesn't exist
        var sql = @"
            CREATE TABLE IF NOT EXISTS metrics (
                id SERIAL PRIMARY KEY,
                timestamp TIMESTAMP NOT NULL,
                command TEXT NOT NULL,
                key_prefix TEXT,
                count INT NOT NULL
            );";
        
        await connection.ExecuteAsync(sql);
    }

    // Batch insert stats
    public async Task SaveMetricsAsync(IEnumerable<MetricLog> metrics)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        var sql = "INSERT INTO metrics (timestamp, command, key_prefix, count) VALUES (@Timestamp, @Command, @KeyPrefix, @Count)";
        await connection.ExecuteAsync(sql, metrics);
    }
}