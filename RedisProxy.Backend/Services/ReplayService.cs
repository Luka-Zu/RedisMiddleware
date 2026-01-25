using StackExchange.Redis;
using RedisProxy.Backend.Data;
using RedisProxy.Backend.MetricModels;

namespace RedisProxy.Backend.Services;

public interface IReplayService
{
    Task StartReplayAsync(DateTime from, DateTime to, string targetHost, int targetPort, double speedMultiplier);
}

public class ReplayService(DatabaseService db, ILogger<ReplayService> logger) : IReplayService
{
    public async Task StartReplayAsync(DateTime from, DateTime to, string targetHost, int targetPort, double speedMultiplier)
    {
        logger.LogInformation($"Starting Replay: {from} -> {to} against {targetHost}:{targetPort} at {speedMultiplier}x speed.");

        // 1. Fetch Logs (We need a method in DatabaseService for this range)
        var logs = await db.GetRequestLogsRangeAsync(from, to);
        if (!logs.Any()) return;

        // 2. Connect to Target
        using var redis = await ConnectionMultiplexer.ConnectAsync($"{targetHost}:{targetPort},allowAdmin=true");
        var database = redis.GetDatabase();

        // 3. Replay Logic
        var startTime = logs.First().Timestamp;
        var playbackStart = DateTime.UtcNow;

        foreach (var log in logs)
        {
            if (string.IsNullOrEmpty(log.Command)) continue;

            // Calculate Delay
            var originalOffset = log.Timestamp - startTime;
            
            // Adjust for speed (e.g., 2x speed means half the wait)
            var adjustedOffset = originalOffset / speedMultiplier;

            var timeToWait = adjustedOffset - (DateTime.UtcNow - playbackStart);

            if (timeToWait > TimeSpan.Zero)
            {
                await Task.Delay(timeToWait);
            }

            // Execute (Fire and Forget)
            try 
            {
                // We reconstruct the command. Note: This is a simplified replay 
                // that works for basic commands (GET, SET, etc). 
                // Complex args parsing is skipped for this demo.
                if (!string.IsNullOrEmpty(log.Key))
                {
                   // Simplistic execution: "COMMAND KEY"
                   // For "SET KEY VALUE", you'd need the raw value stored in DB or parse raw content
                   _ = database.ExecuteAsync(log.Command, log.Key);
                }
                else
                {
                   _ = database.ExecuteAsync(log.Command);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning($"Replay Error on {log.Command}: {ex.Message}");
            }
        }
        
        logger.LogInformation("Replay Completed.");
    }
}