using Microsoft.AspNetCore.SignalR;

namespace RedisProxy.Backend.Hubs;

public class MetricsHub : Hub<IMetricsClient>
{
    
    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
        Console.WriteLine($"Frontend Client Connected: {Context.ConnectionId}");
    }
}