using Microsoft.AspNetCore.Mvc;
using RedisProxy.Backend.Data;

namespace RedisProxy.Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MetricsController(DatabaseService db) : ControllerBase
{
    // GET: api/metrics/server/history?from=2024-12-07T10:00:00Z
    [HttpGet("server/history")]
    public async Task<IActionResult> GetServerHistory([FromQuery] DateTime? from)
    {
        // Default to 1 hour ago if no time provided
        var timeWindow = from ?? DateTime.UtcNow.AddHours(-1);
        
        // Ensure we operate in UTC
        if (timeWindow.Kind == DateTimeKind.Unspecified)
            timeWindow = DateTime.SpecifyKind(timeWindow, DateTimeKind.Utc);

        var data = await db.GetServerMetricsSinceAsync(timeWindow);
        return Ok(data);
    }

    // GET: api/metrics/requests/history?from=2024-12-07T10:00:00Z
    [HttpGet("requests/history")]
    public async Task<IActionResult> GetRequestHistory([FromQuery] DateTime? from)
    {
        // Default to 1 hour ago
        var timeWindow = from ?? DateTime.UtcNow.AddHours(-1);
        
        if (timeWindow.Kind == DateTimeKind.Unspecified)
            timeWindow = DateTime.SpecifyKind(timeWindow, DateTimeKind.Utc);

        var data = await db.GetRequestLogsSinceAsync(timeWindow);
        return Ok(data);
    }
}