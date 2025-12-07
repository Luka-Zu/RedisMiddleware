using System.Net;
using System.Net.Sockets;
using System.Collections.Concurrent;
using RedisProxy.Backend.Data;
using RedisProxy.Backend.Metric;
using RedisProxy.Backend.RespParser;

namespace RedisProxy.Backend;

public class TcpProxyWorker(ILogger<TcpProxyWorker> logger, IRespParser parser, DatabaseService db) : BackgroundService
{

    private const int LocalPort = 6380;
    private const int RemotePort = 6379;
    private const string RemoteHost = "127.0.0.1";

    // Temp stats
    private static ConcurrentDictionary<string, int> _commandStats = new();


    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await db.InitializeDatabaseAsync();
        
        var listener = new TcpListener(IPAddress.Any, LocalPort);
        listener.Start();
        logger.LogInformation($"Proxy listening on {LocalPort}");

        
        // Start the "Background Flusher" task
        // This runs in parallel to the listener
        _ = Task.Run(async () => 
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(5000, stoppingToken); // Wait 5 seconds
                await FlushMetricsToDb();
            }
        }, stoppingToken);
        
        
        while (!stoppingToken.IsCancellationRequested)
        {
            var client = await listener.AcceptTcpClientAsync(stoppingToken);
            _ = HandleClientAsync(client, stoppingToken);
        }
    }

    private async Task FlushMetricsToDb()
    {
        if (_commandStats.IsEmpty) return;

        try 
        {
            var metricsToSave = new List<MetricLog>();
            var timestamp = DateTime.UtcNow;

            foreach (var key in _commandStats.Keys)
            {
                if (_commandStats.TryRemove(key, out int count))
                {
                    metricsToSave.Add(new MetricLog 
                    { 
                        Timestamp = timestamp, 
                        Command = key, 
                        Count = count 
                    });
                }
            }

            if (metricsToSave.Count > 0)
            {
                await db.SaveMetricsAsync(metricsToSave);
                logger.LogInformation($"Saved {metricsToSave.Count} metric records to DB.");
            }
        }
        catch (Exception ex)
        {
            logger.LogError($"Failed to save metrics: {ex.Message}");
        }
    }
    
    private async Task HandleClientAsync(TcpClient clientSocket, CancellationToken ct)
    {
        try 
        {
            using (clientSocket)
            using (var redisSocket = new TcpClient())
            {
                await redisSocket.ConnectAsync(RemoteHost, RemotePort, ct);
                
                using var clientStream = clientSocket.GetStream();
                using var redisStream = redisSocket.GetStream();

                var t1 = TransferDataAsync(clientStream, redisStream, "UPSTREAM", ct);
                var t2 = TransferDataAsync(redisStream, clientStream, "DOWNSTREAM", ct);

                await Task.WhenAny(t1, t2);
            }
        }
        catch (Exception ex)
        {
            logger.LogError($"Error: {ex.Message}");
        }
    }

    private async Task TransferDataAsync(NetworkStream input, NetworkStream output, string direction, CancellationToken ct)
    {
        var buffer = new byte[4096];
        int bytesRead;

        while ((bytesRead = await input.ReadAsync(buffer, ct)) > 0)
        {
            // USE THE INJECTED PARSER
            if (direction == "UPSTREAM")
            {
                var (cmd, key) = parser.ParseRequest(buffer, bytesRead);
                
                if (cmd != null)
                {
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {cmd} {key ?? ""}");
                    Console.ResetColor();

                    _commandStats.AddOrUpdate(cmd, 1, (k, v) => v + 1);
                }
            }

            await output.WriteAsync(buffer, 0, bytesRead, ct);
        }
    }
}