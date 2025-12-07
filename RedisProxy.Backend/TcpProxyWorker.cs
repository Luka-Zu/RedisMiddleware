using System.Net;
using System.Net.Sockets;
using System.Collections.Concurrent;
using RedisProxy.Backend.RespParser;

namespace RedisProxy.Backend;

public class TcpProxyWorker(ILogger<TcpProxyWorker> logger, IRespParser parser) : BackgroundService
{

    private const int LocalPort = 6380;
    private const int RemotePort = 6379;
    private const string RemoteHost = "127.0.0.1";

    // Temp stats
    private static ConcurrentDictionary<string, int> _commandStats = new();


    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var listener = new TcpListener(IPAddress.Any, LocalPort);
        listener.Start();
        logger.LogInformation($"Proxy listening on {LocalPort}");

        while (!stoppingToken.IsCancellationRequested)
        {
            var client = await listener.AcceptTcpClientAsync(stoppingToken);
            _ = HandleClientAsync(client, stoppingToken);
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