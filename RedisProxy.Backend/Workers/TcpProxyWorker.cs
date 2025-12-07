using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using RedisProxy.Backend.Data;
using RedisProxy.Backend.Metric;
using RedisProxy.Backend.RespParser;

namespace RedisProxy.Backend.Workers;

public class TcpProxyWorker(ILogger<TcpProxyWorker> logger, IRespParser parser, DatabaseService db) : BackgroundService
{
    private const int LocalPort = 6380;
    private const int RemotePort = 6379;
    private const string RemoteHost = "127.0.0.1";

    private static ConcurrentDictionary<string, int> _commandStats = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // DB Init handled by DatabaseService or Monitor
        await db.InitializeDatabaseAsync();
        
        var listener = new TcpListener(IPAddress.Any, LocalPort);
        listener.Start();
        logger.LogInformation($"Proxy listening on {LocalPort}");

        // Flusher Logic
        _ = Task.Run(async () => 
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(5000, stoppingToken);
                await FlushCommandMetrics();
            }
        }, stoppingToken);
        
        while (!stoppingToken.IsCancellationRequested)
        {
            var client = await listener.AcceptTcpClientAsync(stoppingToken);
            _ = HandleClientAsync(client, stoppingToken);
        }
    }

    private async Task FlushCommandMetrics()
    {
        if (_commandStats.IsEmpty) return;
        try 
        {
            var logs = new List<CommandMetricLog>();
            var now = DateTime.UtcNow;

            foreach (var key in _commandStats.Keys)
            {
                if (_commandStats.TryRemove(key, out int count))
                {
                    logs.Add(new CommandMetricLog 
                    { 
                        Timestamp = now, 
                        Command = key, 
                        Count = count 
                    });
                }
            }
            if (logs.Count > 0) await db.SaveCommandMetricsAsync(logs);
        }
        catch (Exception ex) { logger.LogError($"Flush error: {ex.Message}"); }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken ct)
    {
        try 
        {
            using (client)
            using (var redis = new TcpClient())
            {
                await redis.ConnectAsync(RemoteHost, RemotePort, ct);
                using var cStream = client.GetStream();
                using var rStream = redis.GetStream();

                await Task.WhenAny(
                    TransferAndAnalyze(cStream, rStream, true, ct),
                    TransferAndAnalyze(rStream, cStream, false, ct)
                );
            }
        }
        catch { /* Connection closed */ }
    }

    private async Task TransferAndAnalyze(NetworkStream input, NetworkStream output, bool isUpstream, CancellationToken ct)
    {
        var buffer = new byte[4096];
        int read;
        while ((read = await input.ReadAsync(buffer, ct)) > 0)
        {
            if (isUpstream)
            {
                var (cmd, _) = parser.ParseRequest(buffer, read);
                if (cmd != null) _commandStats.AddOrUpdate(cmd, 1, (k, v) => v + 1);
            }
            await output.WriteAsync(buffer, 0, read, ct);
        }
    }
}