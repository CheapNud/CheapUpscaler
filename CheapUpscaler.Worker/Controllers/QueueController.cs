using CheapUpscaler.Shared.Services;
using Microsoft.AspNetCore.Mvc;

namespace CheapUpscaler.Worker.Controllers;

/// <summary>
/// REST API for controlling the processing queue in headless deployments
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class QueueController(IUpscaleQueueService queueService, ILogger<QueueController> logger) : ControllerBase
{
    /// <summary>
    /// Start processing queued jobs
    /// </summary>
    [HttpPost("start")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Start()
    {
        queueService.StartQueue();
        logger.LogInformation("Queue started via API");
        return Ok(new { Message = "Queue started", Running = true });
    }

    /// <summary>
    /// Pause queue processing (the currently running job continues)
    /// </summary>
    [HttpPost("stop")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Stop()
    {
        queueService.StopQueue();
        logger.LogInformation("Queue paused via API");
        return Ok(new { Message = "Queue paused", Running = false });
    }

    /// <summary>
    /// Get queue running state and job counts
    /// </summary>
    [HttpGet("status")]
    [ProducesResponseType(typeof(QueueStatusResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStatus()
    {
        var stats = await queueService.GetQueueStatisticsAsync();

        return Ok(new QueueStatusResponse
        {
            Running = !queueService.IsQueuePaused,
            Paused = queueService.IsQueuePaused,
            ActiveCount = stats.RunningCount,
            PendingCount = stats.PendingCount,
            CompletedCount = stats.CompletedCount,
            FailedCount = stats.FailedCount
        });
    }
}

/// <summary>
/// Queue state and job counts
/// </summary>
public record QueueStatusResponse
{
    public bool Running { get; init; }
    public bool Paused { get; init; }
    public int ActiveCount { get; init; }
    public int PendingCount { get; init; }
    public int CompletedCount { get; init; }
    public int FailedCount { get; init; }
}
