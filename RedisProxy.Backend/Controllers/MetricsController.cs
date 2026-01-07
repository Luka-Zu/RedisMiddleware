using Microsoft.AspNetCore.Mvc;
using RedisProxy.Backend.Data;

namespace RedisProxy.Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MetricsController(DatabaseService db) : ControllerBase
{
    [HttpGet("server/history")]
    public async Task<IActionResult> GetServerHistory([FromQuery] DateTime? from)
    {
        var timeWindow = from ?? DateTime.UtcNow.AddHours(-1); // default 1 hour
        
        if (timeWindow.Kind == DateTimeKind.Unspecified)
            timeWindow = DateTime.SpecifyKind(timeWindow, DateTimeKind.Utc);

        var data = await db.GetServerMetricsSinceAsync(timeWindow);
        
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

        var data = await db.GetRequestLogsSinceAsync(timeWindow);
        
        foreach (var item in data)
        {
            if (item.Timestamp.Kind == DateTimeKind.Unspecified)
            {
                item.Timestamp = DateTime.SpecifyKind(item.Timestamp, DateTimeKind.Utc);
            }
        }
        return Ok(data);
    }
}