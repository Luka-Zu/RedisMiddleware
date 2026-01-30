using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace RedisProxy.Backend.Hubs;

[Authorize]
public class MetricsHub : Hub<IMetricsClient>
{
    
    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
        Console.WriteLine($"Frontend Client Connected: {Context.ConnectionId}");
    }
}