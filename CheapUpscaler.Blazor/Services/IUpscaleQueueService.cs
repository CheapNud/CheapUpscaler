using CheapUpscaler.Blazor.Models;

namespace CheapUpscaler.Blazor.Services;

/// <summary>
/// Service interface for managing the upscale job queue
/// </summary>
public interface IUpscaleQueueService
{
    /// <summary>Fired when job progress changes</summary>
    event EventHandler<UpscaleProgressEventArgs>? ProgressChanged;

    /// <summary>Fired when job status changes (started, completed, failed, etc.)</summary>
    event EventHandler<UpscaleProgressEventArgs>? StatusChanged;

    /// <summary>Fired when queue paused/started state changes</summary>
    event EventHandler<bool>? QueueStatusChanged;

    /// <summary>Is the queue currently paused</summary>
    bool IsQueuePaused { get; }

    /// <summary>Start processing queued jobs</summary>
    void StartQueue();

    /// <summary>Pause queue processing (current job continues)</summary>
    void StopQueue();

    // Job management
    Task<Guid> AddJobAsync(UpscaleJob job);
    Task<bool> PauseJobAsync(Guid jobId);
    Task<bool> ResumeJobAsync(Guid jobId);
    Task<bool> CancelJobAsync(Guid jobId);
    Task<bool> RetryJobAsync(Guid jobId);
    Task<bool> DeleteJobAsync(Guid jobId);

    // Job queries
    Task<UpscaleJob?> GetJobAsync(Guid jobId);
    Task<IEnumerable<UpscaleJob>> GetAllJobsAsync();
    Task<IEnumerable<UpscaleJob>> GetActiveJobsAsync();
    Task<IEnumerable<UpscaleJob>> GetCompletedJobsAsync();
    Task<IEnumerable<UpscaleJob>> GetFailedJobsAsync();
    Task<QueueStatistics> GetQueueStatisticsAsync();

    // Bulk operations
    Task<int> ClearCompletedJobsAsync();
    Task<int> ClearAllJobsAsync();
}
