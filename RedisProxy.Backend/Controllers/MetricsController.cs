using Microsoft.AspNetCore.Mvc;
using RedisProxy.Backend.Data;
using RedisProxy.Backend.Services;

namespace RedisProxy.Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MetricsController(DatabaseService dbService, IKeyspaceService keyspaceService) : ControllerBase
{
    [HttpGet("server/history")]
    public async Task<IActionResult> GetServerHistory([FromQuery] DateTime? from)
    {
        var timeWindow = from ?? DateTime.UtcNow.AddHours(-1); // default 1 hour
        
        if (timeWindow.Kind == DateTimeKind.Unspecified)
            timeWindow = DateTime.SpecifyKind(timeWindow, DateTimeKind.Utc);

        var data = await dbService.GetServerMetricsSinceAsync(timeWindow);
        
        foreach (var item in data)
        {
            if (item.Timestamp.Kind == DateTimeKind.Unspecified)
            {
                item.Timestamp = DateTime.SpecifyKind(item.Timestamp, DateTimeKind.Utc);
            }
        }
        return Ok(data);
    }

    [HttpGet("requests/history")]
    public async Task<IActionResult> GetRequestHistory([FromQuery] DateTime? from)
    {
        var timeWindow = from ?? DateTime.UtcNow.AddHours(-1); // Default to 1 hour ago
        
        if (timeWindow.Kind == DateTimeKind.Unspecified)
            timeWindow = DateTime.SpecifyKind(timeWindow, DateTimeKind.Utc);

        var data = await dbService.GetRequestLogsSinceAsync(timeWindow);
        
        foreach (var item in data)
        {
            if (item.Timestamp.Kind == DateTimeKind.Unspecified)
            {
                item.Timestamp = DateTime.SpecifyKind(item.Timestamp, DateTimeKind.Utc);
            }
        }
        return Ok(data);
    }
    
    
    [HttpGet("commands/stats")]
    public async Task<IActionResult> GetCommandStats([FromQuery] DateTime from)
    {
        // Safety check: if 'from' is default, use 1 hour ago
        if (from == default) from = DateTime.UtcNow.AddHours(-1);

        var stats = await dbService.GetCommandStatsAsync(from);
        return Ok(stats);
    }
    
    [HttpGet("keys/hot")]
    public async Task<IActionResult> GetHotKeys([FromQuery] DateTime from)
    {
        // Default to 1 hour ago if no time provided
        if (from == default) from = DateTime.UtcNow.AddHours(-1);
    
        var stats = await dbService.GetHotKeysAsync(from);
        return Ok(stats);
    }
    
    [HttpGet("keyspace")]
    public async Task<IActionResult> GetKeyspace([FromQuery] DateTime? from)
    {
        // Default to 1 hour if not provided
        var timeWindow = from ?? DateTime.UtcNow.AddHours(-1);
        
        if (timeWindow.Kind == DateTimeKind.Unspecified)
            timeWindow = DateTime.SpecifyKind(timeWindow, DateTimeKind.Utc);

        var snapshot = await keyspaceService.GetKeyspaceSnapshotAsync(timeWindow);
        return Ok(snapshot);
    }
    
}