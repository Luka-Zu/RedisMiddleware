using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.SignalR;
using RedisProxy.Backend.Data;
using RedisProxy.Backend.Hubs;
using RedisProxy.Backend.Metric;
using RedisProxy.Backend.MetricModels;
using RedisProxy.Backend.RespParser;

namespace RedisProxy.Backend.Workers;

public class TcpProxyWorker(ILogger<TcpProxyWorker> logger, 
    IRespParser parser, 
    DatabaseService db,
    IHubContext<MetricsHub, IMetricsClient> hub) : BackgroundService
{
    private const int LocalPort = 6380;
    private const int RemotePort = 6379;
    private const string RemoteHost = "127.0.0.1";

    // BUFFER: Holds individual logs before saving
    private readonly ConcurrentQueue<RequestLog> _logBuffer = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await db.InitializeDatabaseAsync();
        var listener = new TcpListener(IPAddress.Any, LocalPort);
        listener.Start();
        logger.LogInformation($"Detailed Proxy listening on {LocalPort}");

        // Background Flusher
        _ = Task.Run(async () => 
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(3000, stoppingToken); // Flush every 3 seconds
                await FlushLogs();
            }
        }, stoppingToken);
        
        while (!stoppingToken.IsCancellationRequested)
        {
            var client = await listener.AcceptTcpClientAsync(stoppingToken);
            _ = HandleClientAsync(client, stoppingToken);
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken ct)
    {
        try 
        {
            using (client)
            using (var redis = new TcpClient())
            {
                await redis.ConnectAsync(RemoteHost, RemotePort, ct);
                await using var clientStream = client.GetStream();
                await using var redisStream = redis.GetStream();

                var requestQueue = new ConcurrentQueue<RequestContext>();

                await Task.WhenAny(
                    HandleUpstream(clientStream, redisStream, requestQueue, ct),
                    HandleDownstream(redisStream, clientStream, requestQueue, ct)
                );
            }
        }
        catch { /* Connection closed */ }
    }

    private async Task HandleUpstream(NetworkStream input, NetworkStream output, ConcurrentQueue<RequestContext> queue, CancellationToken ct)
    {
        var buffer = new byte[8192];
        int bytesRead;

        while ((bytesRead = await input.ReadAsync(buffer, ct)) > 0)
        {
            var (cmd, key) = parser.ParseRequest(buffer, bytesRead);
            
            if (!string.IsNullOrEmpty(cmd))
            {
                queue.Enqueue(new RequestContext
                {
                    Command = cmd,
                    Key = key ?? "",
                    PayloadSize = bytesRead
                });
            }

            await output.WriteAsync(buffer, 0, bytesRead, ct);
        }
    }

    private async Task HandleDownstream(NetworkStream input, NetworkStream output, ConcurrentQueue<RequestContext> queue, CancellationToken ct)
    {
        var buffer = new byte[8192];
        int bytesRead;

        while ((bytesRead = await input.ReadAsync(buffer, ct)) > 0)
        {
            // If we have a pending request, we match this response to it
            if (queue.TryDequeue(out var request))
            {
                AnalyzeAndLog(request, buffer, bytesRead);
            }

            await output.WriteAsync(buffer, 0, bytesRead, ct);
        }
    }

    private void AnalyzeAndLog(RequestContext request, byte[] buffer, int length)
    {
        long endTimestamp = Stopwatch.GetTimestamp();
        double latencyMs = (endTimestamp - request.StartTimestamp) * 1000.0 / Stopwatch.Frequency;

        bool isSuccess = true;
        bool isHit = true; // Default to true

        if (length > 0)
        {
            byte firstByte = buffer[0];

            // Redis Protocol: '-' indicates an Error (e.g., -WRONGTYPE)
            if (firstByte == (byte)'-') 
            {
                isSuccess = false;
            }

            // Hit/Miss Logic (Specific to GET)
            if (request.Command == "GET")
            {
                // $-1 means Null (Miss)
                if (length >= 3 && buffer[0] == '$' && buffer[1] == '-' && buffer[2] == '1')
                {
                    isHit = false;
                }
            }
        }

        _logBuffer.Enqueue(new RequestLog
        {
            Timestamp = DateTime.UtcNow,
            Command = request.Command,
            Key = request.Key,
            PayloadSize = request.PayloadSize,
            LatencyMs = Math.Round(latencyMs, 3), // Round to 3 decimal places
            IsSuccess = isSuccess,
            IsHit = isHit
        });
    }

    private async Task FlushLogs()
    {
        if (_logBuffer.IsEmpty) return;

        var logsToSave = new List<RequestLog>();
        
        while (_logBuffer.TryDequeue(out var log))
        {
            logsToSave.Add(log);
            if (logsToSave.Count >= 5000) break; 
        }

        if (logsToSave.Count > 0)
        {
            try 
            {
                await db.SaveRequestLogsAsync(logsToSave);
                // We send the latest batch to Angular immediately
                await hub.Clients.All.ReceiveRequestLogUpdate(logsToSave);
                logger.LogInformation($"Flushed {logsToSave.Count} request logs.");
            }
            catch (Exception ex)
            {
                logger.LogError($"Log Flush Failed: {ex.Message}");
            }
        }
    }
}