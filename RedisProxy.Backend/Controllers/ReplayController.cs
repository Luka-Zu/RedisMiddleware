using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RedisProxy.Backend.DTOs;
using RedisProxy.Backend.Services;

namespace RedisProxy.Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ReplayController(IReplayService replayService) : ControllerBase
{
    

    [HttpPost("start")]
    public IActionResult StartReplay([FromBody] ReplayRequestDTO request)
    {
        _ = Task.Run(() => replayService.StartReplayAsync(
            request.From, request.To, request.TargetHost, request.TargetPort, request.Speed
        ));

        return Accepted(new { Message = "Replay started in background" });
    }
}