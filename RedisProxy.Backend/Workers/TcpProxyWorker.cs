using System.Buffers;
using System.Buffers.Text;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;
using Microsoft.AspNetCore.SignalR;
using RedisProxy.Backend.Data;
using RedisProxy.Backend.Hubs;
using RedisProxy.Backend.Metric;
using RedisProxy.Backend.MetricModels;
using RedisProxy.Backend.RespParser;
using RedisProxy.Backend.Services;

namespace RedisProxy.Backend.Workers;

public class TcpProxyWorker : BackgroundService
{
    private readonly ILogger<TcpProxyWorker> _logger;
    private readonly IRespParser _parser;
    private readonly DatabaseService _db;
    private readonly IAdvisoryService _advisoryService;
    private readonly IKeyspaceService _keyspaceService;
    private readonly IHubContext<MetricsHub, IMetricsClient> _hub;

    private const int LocalPort = 6380;
    private const int RemotePort = 6379;
    private const string RemoteHost = "127.0.0.1";
    private const int BufferSize = 65536;

    // OPTIMIZATION: Use Channel instead of ConcurrentQueue for efficient buffering
    private readonly Channel<RequestLog> _logChannel;

    public TcpProxyWorker(ILogger<TcpProxyWorker> logger,
        IRespParser parser,
        DatabaseService db,
        IAdvisoryService advisoryService,
        IKeyspaceService keyspaceService,
        IHubContext<MetricsHub, IMetricsClient> hub)
    {
        _logger = logger;
        _parser = parser;
        _db = db;
        _advisoryService = advisoryService;
        _keyspaceService = keyspaceService;
        _hub = hub;
        
        // Unbounded channel to handle high throughput spikes without blocking
        _logChannel = Channel.CreateUnbounded<RequestLog>(new UnboundedChannelOptions 
        {
            SingleReader = true, // We have one flusher loop
            SingleWriter = false // Multiple client connections write to it
        });
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _db.InitializeDatabaseAsync();
        var listener = new TcpListener(IPAddress.Any, LocalPort);
        listener.Start();
        _logger.LogInformation($"[High-Perf Proxy] Listening on {LocalPort}");

        // Start the background flusher
        _ = ProcessLogsAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            var client = await listener.AcceptTcpClientAsync(stoppingToken);
            // Fire and forget, but pass token
            _ = HandleClientAsync(client, stoppingToken);
        }
    }

    // OPTIMIZATION: Zero-allocation parser using Spans
    // Instead of allocating a huge string just to check for a "$", we scan the bytes directly.
    private int EstimateRespPayloadSize(ReadOnlySpan<byte> span)
    {
        int maxDeclaredSize = 0;
        int i = 0;

        while (i < span.Length)
        {
            // Find next '$' character (byte 36)
            int dollarIndex = span.Slice(i).IndexOf((byte)'$');
            if (dollarIndex == -1) break;

            i += dollarIndex + 1; // Move past the '$'

            // Try to parse the number immediately following '$'
            if (Utf8Parser.TryParse(span.Slice(i), out int size, out int bytesConsumed))
            {
                if (size > maxDeclaredSize) maxDeclaredSize = size;
                i += bytesConsumed;
            }
        }
        
        return Math.Max(maxDeclaredSize, span.Length);
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken ct)
    {
        try
        {
            using (client)
            using (var redis = new TcpClient())
            {
                // Disable Nagle's algorithm for lower latency
                client.NoDelay = true; 
                redis.NoDelay = true;

                await redis.ConnectAsync(RemoteHost, RemotePort, ct);
                
                await using var clientStream = client.GetStream();
                await using var redisStream = redis.GetStream();

                var requestQueue = new ConcurrentQueue<RequestContext>();

                // Run bi-directional copying
                await Task.WhenAny(
                    HandleStreamAsync(clientStream, redisStream, requestQueue, true, ct),
                    HandleStreamAsync(redisStream, clientStream, requestQueue, false, ct)
                );
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogDebug($"Connection closed: {ex.Message}");
        }
    }

    // Combined method for Upstream/Downstream to reduce code duplication
    private async Task HandleStreamAsync(NetworkStream input, NetworkStream output, 
        ConcurrentQueue<RequestContext> queue, bool isUpstream, CancellationToken ct)
    {
        // OPTIMIZATION: Rent buffer from shared pool to reduce GC pressure
        byte[] buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
        
        try
        {
            int bytesRead;
            while ((bytesRead = await input.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
            {
                if (isUpstream)
                {
                    // UPSTREAM LOGIC: Parse Command
                    ProcessUpstream(buffer.AsSpan(0, bytesRead), queue);
                }
                else
                {
                    // DOWNSTREAM LOGIC: Calculate Latency
                    ProcessDownstream(buffer.AsSpan(0, bytesRead), queue);
                }

                await output.WriteAsync(buffer, 0, bytesRead, ct);
            }
        }
        finally
        {
            // CRITICAL: Always return the buffer to the pool!
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private void ProcessUpstream(ReadOnlySpan<byte> data, ConcurrentQueue<RequestContext> queue)
    {
        // Convert Span to Array only where strictly necessary for the Parser interface
        // ideally Parser should accept Span too, but assuming it needs array:
        var (cmd, key) = _parser.ParseRequest(data.ToArray(), data.Length);

        if (!string.IsNullOrEmpty(cmd))
        {
            int logicalSize = EstimateRespPayloadSize(data);
            
            // We only convert to string IF we found a command, saving CPU on empty packets
            // For massive payloads, you might want to truncate this string to save RAM.
            string rawContent = System.Text.Encoding.UTF8.GetString(data);

            queue.Enqueue(new RequestContext
            {
                Command = cmd,
                Key = key ?? "",
                PayloadSize = logicalSize,
                RawContent = rawContent,
                StartTimestamp = Stopwatch.GetTimestamp() // High-precision timer
            });
        }
    }

    private void ProcessDownstream(ReadOnlySpan<byte> data, ConcurrentQueue<RequestContext> queue)
    {
        // Match response to the oldest pending request
        if (queue.TryDequeue(out var request))
        {
            long endTimestamp = Stopwatch.GetTimestamp();
            double latencyMs = (endTimestamp - request.StartTimestamp) * 1000.0 / Stopwatch.Frequency;

            bool isSuccess = data.Length > 0 && data[0] != (byte)'-'; // '-' is Error
            
            // Check for Redis Null ($-1) efficiently
            bool isHit = true;
            if (request.Command == "GET" && data.Length >= 3)
            {
                if (data[0] == (byte)'$' && data[1] == (byte)'-' && data[2] == (byte)'1')
                {
                    isHit = false;
                }
            }

            // Write to Channel (Non-blocking)
            _logChannel.Writer.TryWrite(new RequestLog
            {
                Timestamp = DateTime.UtcNow,
                Command = request.Command,
                Key = request.Key,
                PayloadSize = request.PayloadSize,
                LatencyMs = Math.Round(latencyMs, 3),
                IsSuccess = isSuccess,
                IsHit = isHit,
                RawContent = request.RawContent
            });
        }
    }

    // The Consumer Loop
    private async Task ProcessLogsAsync(CancellationToken ct)
    {
        var batch = new List<RequestLog>(500); // Pre-allocate capacity

        // Smart Batching Loop
        while (await _logChannel.Reader.WaitToReadAsync(ct))
        {
            while (_logChannel.Reader.TryRead(out var log))
            {
                batch.Add(log);
                if (batch.Count >= 100) break; // Process in smaller chunks for responsiveness
            }

            if (batch.Count > 0)
            {
                await SaveAndBroadcastBatch(batch);
                batch.Clear();
            }
            
            // Small safeguard to prevent CPU spinning if data comes in insanely fast
            // though Channel usually handles backpressure well.
            if (batch.Count == 0) await Task.Delay(100, ct); 
        }
    }

    private async Task SaveAndBroadcastBatch(List<RequestLog> logs)
    {
        try
        {
            // 1. Save to DB
            await _db.SaveRequestLogsAsync(logs);

            // 2. Broadcast Logs to UI
            await _hub.Clients.All.ReceiveRequestLogUpdate(logs);

            // 3. Analyze for Advisories
            var newAdvisories = new List<Advisory>();
            foreach (var log in logs)
            {
                newAdvisories.AddRange(_advisoryService.AnalyzeLog(log, log.RawContent ?? ""));
            }
            if (newAdvisories.Count > 0)
            {
                await _hub.Clients.All.ReceiveAdvisories(newAdvisories);
            }

            // 4. Update Keyspace (Trigger UI refresh logic)
            // Simplified for thesis: Just broadcast the keys that changed
            var keys = logs.Select(l => l.Key).Where(k => !string.IsNullOrEmpty(k)).Distinct();
            if (keys.Any())
            {
                var miniSnapshot = _keyspaceService.BuildTree(keys);
                await _hub.Clients.All.ReceiveKeyspaceUpdate(miniSnapshot);
            }
            
            _logger.LogDebug($"Processed batch of {logs.Count} logs");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Batch processing failed: {ex.Message}");
        }
    }
}