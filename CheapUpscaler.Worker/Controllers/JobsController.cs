using CheapUpscaler.Shared.Models;
using CheapUpscaler.Worker.Services;
using Microsoft.AspNetCore.Mvc;

namespace CheapUpscaler.Worker.Controllers;

/// <summary>
/// REST API for managing upscale jobs
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class JobsController(WorkerQueueService queueService, ILogger<JobsController> logger) : ControllerBase
{
    /// <summary>
    /// Submit a new upscale job
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(JobCreatedResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateJob([FromBody] CreateJobRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.InputPath))
        {
            return BadRequest(new { Error = "InputPath is required" });
        }

        if (!System.IO.File.Exists(request.InputPath))
        {
            return BadRequest(new { Error = $"Input file not found: {request.InputPath}" });
        }

        var outputPath = request.OutputPath;
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            var inputDir = Path.GetDirectoryName(request.InputPath) ?? "";
            var inputName = Path.GetFileNameWithoutExtension(request.InputPath);
            outputPath = Path.Combine(inputDir, $"{inputName}_upscaled.mp4");
        }

        var job = new UpscaleJob
        {
            JobId = Guid.NewGuid(),
            JobName = request.Name ?? Path.GetFileName(request.InputPath),
            SourceVideoPath = request.InputPath,
            OutputPath = outputPath,
            UpscaleType = request.UpscaleType,
            SettingsJson = request.SettingsJson ?? "{}",
            CreatedAt = DateTime.UtcNow
        };

        var jobId = await queueService.AddJobAsync(job);
        logger.LogInformation("Job {JobId} created via API", jobId);

        return CreatedAtAction(nameof(GetJob), new { id = jobId }, new JobCreatedResponse
        {
            JobId = jobId,
            Status = "Pending",
            Message = "Job queued successfully"
        });
    }

    /// <summary>
    /// Get job status by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(JobStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetJob(Guid id)
    {
        var job = await queueService.GetJobAsync(id);
        if (job == null)
        {
            return NotFound(new { Error = $"Job {id} not found" });
        }

        return Ok(MapToResponse(job));
    }

    /// <summary>
    /// List all jobs
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<JobStatusResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllJobs([FromQuery] string? status = null)
    {
        var jobs = await queueService.GetAllJobsAsync();

        if (!string.IsNullOrWhiteSpace(status) &&
            Enum.TryParse<UpscaleJobStatus>(status, true, out var filterStatus))
        {
            jobs = jobs.Where(j => j.Status == filterStatus);
        }

        return Ok(jobs.Select(MapToResponse));
    }

    /// <summary>
    /// Cancel a job
    /// </summary>
    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CancelJob(Guid id)
    {
        var success = await queueService.CancelJobAsync(id);
        if (!success)
        {
            var job = await queueService.GetJobAsync(id);
            if (job == null)
            {
                return NotFound(new { Error = $"Job {id} not found" });
            }
            return BadRequest(new { Error = $"Cannot cancel job in status: {job.Status}" });
        }

        logger.LogInformation("Job {JobId} cancelled via API", id);
        return Ok(new { Message = "Job cancelled" });
    }

    /// <summary>
    /// Retry a failed job
    /// </summary>
    [HttpPost("{id:guid}/retry")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RetryJob(Guid id)
    {
        var success = await queueService.RetryJobAsync(id);
        if (!success)
        {
            var job = await queueService.GetJobAsync(id);
            if (job == null)
            {
                return NotFound(new { Error = $"Job {id} not found" });
            }
            return BadRequest(new { Error = $"Cannot retry job in status: {job.Status}" });
        }

        logger.LogInformation("Job {JobId} retried via API", id);
        return Ok(new { Message = "Job queued for retry" });
    }

    /// <summary>
    /// Delete a job
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteJob(Guid id)
    {
        var success = await queueService.DeleteJobAsync(id);
        if (!success)
        {
            return NotFound(new { Error = $"Job {id} not found" });
        }

        logger.LogInformation("Job {JobId} deleted via API", id);
        return NoContent();
    }

    /// <summary>
    /// Download the output file for a completed job
    /// </summary>
    [HttpGet("{id:guid}/download")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DownloadOutput(Guid id)
    {
        var job = await queueService.GetJobAsync(id);
        if (job == null)
        {
            return NotFound(new { Error = $"Job {id} not found" });
        }

        if (job.Status != UpscaleJobStatus.Completed)
        {
            return BadRequest(new { Error = $"Job is not completed (status: {job.Status})" });
        }

        if (string.IsNullOrEmpty(job.OutputPath) || !System.IO.File.Exists(job.OutputPath))
        {
            return NotFound(new { Error = "Output file not found" });
        }

        var fileName = Path.GetFileName(job.OutputPath);
        var contentType = "video/mp4";

        // Determine content type based on extension
        var ext = Path.GetExtension(job.OutputPath).ToLowerInvariant();
        contentType = ext switch
        {
            ".mkv" => "video/x-matroska",
            ".avi" => "video/x-msvideo",
            ".webm" => "video/webm",
            ".mov" => "video/quicktime",
            _ => "video/mp4"
        };

        logger.LogInformation("Downloading output for job {JobId}: {FileName}", id, fileName);

        var stream = new FileStream(job.OutputPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return File(stream, contentType, fileName);
    }

    /// <summary>
    /// Get queue statistics
    /// </summary>
    [HttpGet("stats")]
    [ProducesResponseType(typeof(QueueStatistics), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStats()
    {
        var stats = await queueService.GetQueueStatisticsAsync();
        return Ok(stats);
    }

    private static JobStatusResponse MapToResponse(UpscaleJob job) => new()
    {
        JobId = job.JobId,
        Name = job.JobName,
        Status = job.Status.ToString(),
        UpscaleType = job.UpscaleType.ToString(),
        InputPath = job.SourceVideoPath,
        OutputPath = job.OutputPath,
        Progress = job.ProgressPercentage,
        Error = job.LastError,
        CreatedAt = job.CreatedAt,
        StartedAt = job.StartedAt,
        CompletedAt = job.CompletedAt
    };
}

/// <summary>
/// Request to create a new upscale job
/// </summary>
public record CreateJobRequest
{
    /// <summary>Path to input video file</summary>
    public required string InputPath { get; init; }

    /// <summary>Path for output video file (optional, auto-generated if not specified)</summary>
    public string? OutputPath { get; init; }

    /// <summary>Type of upscaling to perform</summary>
    public UpscaleType UpscaleType { get; init; } = UpscaleType.RealEsrgan;

    /// <summary>Job name (optional)</summary>
    public string? Name { get; init; }

    /// <summary>JSON settings for the upscale type (optional)</summary>
    public string? SettingsJson { get; init; }
}

/// <summary>
/// Response after creating a job
/// </summary>
public record JobCreatedResponse
{
    public Guid JobId { get; init; }
    public string Status { get; init; } = "";
    public string Message { get; init; } = "";
}

/// <summary>
/// Job status response
/// </summary>
public record JobStatusResponse
{
    public Guid JobId { get; init; }
    public string? Name { get; init; }
    public string Status { get; init; } = "";
    public string UpscaleType { get; init; } = "";
    public string InputPath { get; init; } = "";
    public string OutputPath { get; init; } = "";
    public double Progress { get; init; }
    public string? Error { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
}
