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

        var logs = await db.GetRequestLogsRangeAsync(from, to);
        if (!logs.Any()) return;

        using var redis = await ConnectionMultiplexer.ConnectAsync($"{targetHost}:{targetPort},allowAdmin=true");
        var database = redis.GetDatabase();

        var startTime = logs.First().Timestamp;
        var playbackStart = DateTime.UtcNow;

        foreach (var log in logs)
        {
            if (string.IsNullOrEmpty(log.Command)) continue;

            var originalOffset = log.Timestamp - startTime;
            
            var adjustedOffset = originalOffset / speedMultiplier;

            var timeToWait = adjustedOffset - (DateTime.UtcNow - playbackStart);

            if (timeToWait > TimeSpan.Zero)
            {
                await Task.Delay(timeToWait);
            }

            try 
            {
                if (!string.IsNullOrEmpty(log.Key))
                {
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