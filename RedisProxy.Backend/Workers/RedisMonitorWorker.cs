using Microsoft.AspNetCore.SignalR;
using RedisProxy.Backend.Hubs;
using RedisProxy.Backend.MetricModels;
using RedisProxy.Backend.Services;

namespace RedisProxy.Backend.Workers;
using System.Net.Sockets;
using System.Text;
using Data;

public class RedisMonitorWorker(ILogger<RedisMonitorWorker> logger, 
    DatabaseService db,
    IHubContext<MetricsHub, IMetricsClient> hub, IConfiguration config) : BackgroundService
{
    private readonly string _remoteHost = config["RedisSettings:Host"] ?? "127.0.0.1";
    private const int RemotePort = 6379;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
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

        return RedisInfoParser.Parse(response);
    }

}