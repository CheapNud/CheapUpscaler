using System.Collections.Concurrent;
using CheapUpscaler.Components.Services;
using CheapUpscaler.Shared.Data;
using CheapUpscaler.Shared.Models;
using CheapUpscaler.Shared.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CheapUpscaler.Worker.Services;

/// <summary>
/// Queue service for processing upscale jobs with Blazor UI support
/// Implements IUpscaleQueueService for UI integration
/// </summary>
public class WorkerQueueService : BackgroundService, IUpscaleQueueService
{
    private readonly IBackgroundTaskQueue _taskQueue;
    private readonly IUpscaleJobRepository _repository;
    private readonly IWorkerProcessorService _processor;
    private readonly ILogger<WorkerQueueService> _logger;
    private readonly ConcurrentDictionary<Guid, UpscaleJob> _jobs = new();
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _jobCancellations = new();
    private readonly SemaphoreSlim _processingSemaphore;
    private readonly int _maxConcurrentJobs;
    private volatile bool _isQueuePaused = true; // Default to paused
    private bool _isInitialized;

    public event EventHandler<UpscaleProgressEventArgs>? ProgressChanged;
    public event EventHandler<UpscaleProgressEventArgs>? StatusChanged;
    public event EventHandler<bool>? QueueStatusChanged;

    public bool IsQueuePaused => _isQueuePaused;

    public WorkerQueueService(
        IBackgroundTaskQueue taskQueue,
        IUpscaleJobRepository repository,
        IWorkerProcessorService processor,
        IConfiguration configuration,
        ILogger<WorkerQueueService> logger)
    {
        _taskQueue = taskQueue;
        _repository = repository;
        _processor = processor;
        _logger = logger;
        _maxConcurrentJobs = configuration.GetValue("Worker:MaxConcurrentJobs", 1);
        _processingSemaphore = new SemaphoreSlim(_maxConcurrentJobs, _maxConcurrentJobs);
    }

    public void StartQueue()
    {
        _isQueuePaused = false;
        QueueStatusChanged?.Invoke(this, false);
        _logger.LogInformation("Queue started");
    }

    public void StopQueue()
    {
        _isQueuePaused = true;
        QueueStatusChanged?.Invoke(this, true);
        _logger.LogInformation("Queue paused");
    }

    /// <summary>
    /// Add a new job to the queue
    /// </summary>
    public async Task<Guid> AddJobAsync(UpscaleJob job)
    {
        job.QueuedAt = DateTime.UtcNow;
        job.Status = UpscaleJobStatus.Pending;

        await _repository.AddAsync(job);
        _jobs[job.JobId] = job;

        await _taskQueue.QueueBackgroundWorkItemAsync(async token =>
        {
            await ProcessJobAsync(job.JobId, token);
        });

        _logger.LogInformation("Job {JobId} added to queue", job.JobId);
        return job.JobId;
    }

    /// <summary>
    /// Get job by ID
    /// </summary>
    public Task<UpscaleJob?> GetJobAsync(Guid jobId)
    {
        _jobs.TryGetValue(jobId, out var job);
        return Task.FromResult(job);
    }

    /// <summary>
    /// Get all jobs
    /// </summary>
    public Task<IEnumerable<UpscaleJob>> GetAllJobsAsync()
    {
        return Task.FromResult(_jobs.Values.OrderByDescending(j => j.CreatedAt).AsEnumerable());
    }

    /// <summary>
    /// Cancel a job
    /// </summary>
    public async Task<bool> CancelJobAsync(Guid jobId)
    {
        if (_jobs.TryGetValue(jobId, out var job) &&
            job.Status is UpscaleJobStatus.Pending or UpscaleJobStatus.Running or UpscaleJobStatus.Paused)
        {
            // Cancel the processing token if job is running
            if (_jobCancellations.TryRemove(jobId, out var cts))
            {
                try
                {
                    await cts.CancelAsync();
                    _logger.LogDebug("Cancellation token triggered for job {JobId}", jobId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error cancelling job {JobId} token", jobId);
                }
                finally
                {
                    cts.Dispose();
                }
            }

            job.Status = UpscaleJobStatus.Cancelled;
            job.LastUpdatedAt = DateTime.UtcNow;
            job.CompletedAt = DateTime.UtcNow;
            await _repository.UpdateAsync(job);
            OnStatusChanged(job);
            _logger.LogInformation("Job {JobId} cancelled", jobId);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Pause a running job
    /// </summary>
    public async Task<bool> PauseJobAsync(Guid jobId)
    {
        if (_jobs.TryGetValue(jobId, out var job) && job.Status == UpscaleJobStatus.Running)
        {
            job.Status = UpscaleJobStatus.Paused;
            job.LastUpdatedAt = DateTime.UtcNow;
            await _repository.UpdateAsync(job);
            OnStatusChanged(job);
            _logger.LogInformation("Job {JobId} paused", jobId);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Resume a paused job
    /// </summary>
    public async Task<bool> ResumeJobAsync(Guid jobId)
    {
        if (_jobs.TryGetValue(jobId, out var job) && job.Status == UpscaleJobStatus.Paused)
        {
            job.Status = UpscaleJobStatus.Pending;
            job.LastUpdatedAt = DateTime.UtcNow;
            await _repository.UpdateAsync(job);
            OnStatusChanged(job);
            _logger.LogInformation("Job {JobId} resumed", jobId);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Retry a failed job
    /// </summary>
    public async Task<bool> RetryJobAsync(Guid jobId)
    {
        if (_jobs.TryGetValue(jobId, out var job) &&
            job.Status is UpscaleJobStatus.Failed or UpscaleJobStatus.Cancelled)
        {
            job.Status = UpscaleJobStatus.Pending;
            job.ProgressPercentage = 0;
            job.CurrentFrame = 0;
            job.LastError = null;
            job.ErrorStackTrace = null;
            job.RetryCount++;
            job.LastUpdatedAt = DateTime.UtcNow;
            job.CompletedAt = null;

            await _repository.UpdateAsync(job);

            await _taskQueue.QueueBackgroundWorkItemAsync(async token =>
            {
                await ProcessJobAsync(job.JobId, token);
            });

            _logger.LogInformation("Job {JobId} queued for retry (attempt {RetryCount})", jobId, job.RetryCount);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Delete a job
    /// </summary>
    public async Task<bool> DeleteJobAsync(Guid jobId)
    {
        if (_jobs.TryRemove(jobId, out _))
        {
            await _repository.DeleteAsync(jobId);
            _logger.LogInformation("Job {JobId} deleted", jobId);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Get queue statistics
    /// </summary>
    public Task<QueueStatistics> GetQueueStatisticsAsync()
    {
        var stats = new QueueStatistics
        {
            PendingCount = _jobs.Values.Count(j => j.Status == UpscaleJobStatus.Pending),
            RunningCount = _jobs.Values.Count(j => j.Status == UpscaleJobStatus.Running),
            CompletedCount = _jobs.Values.Count(j => j.Status == UpscaleJobStatus.Completed),
            FailedCount = _jobs.Values.Count(j => j.Status is UpscaleJobStatus.Failed or UpscaleJobStatus.Cancelled)
        };
        return Task.FromResult(stats);
    }

    /// <summary>
    /// Get active jobs (pending, running, paused)
    /// </summary>
    public Task<IEnumerable<UpscaleJob>> GetActiveJobsAsync()
    {
        var activeStatuses = new[] { UpscaleJobStatus.Pending, UpscaleJobStatus.Running, UpscaleJobStatus.Paused };
        return Task.FromResult(_jobs.Values
            .Where(j => activeStatuses.Contains(j.Status))
            .OrderByDescending(j => j.CreatedAt)
            .AsEnumerable());
    }

    /// <summary>
    /// Get completed jobs
    /// </summary>
    public Task<IEnumerable<UpscaleJob>> GetCompletedJobsAsync()
    {
        return Task.FromResult(_jobs.Values
            .Where(j => j.Status == UpscaleJobStatus.Completed)
            .OrderByDescending(j => j.CompletedAt)
            .AsEnumerable());
    }

    /// <summary>
    /// Get failed jobs (failed and cancelled)
    /// </summary>
    public Task<IEnumerable<UpscaleJob>> GetFailedJobsAsync()
    {
        var failedStatuses = new[] { UpscaleJobStatus.Failed, UpscaleJobStatus.Cancelled };
        return Task.FromResult(_jobs.Values
            .Where(j => failedStatuses.Contains(j.Status))
            .OrderByDescending(j => j.CompletedAt)
            .AsEnumerable());
    }

    /// <summary>
    /// Clear completed jobs
    /// </summary>
    public async Task<int> ClearCompletedJobsAsync()
    {
        var completedJobs = _jobs.Values.Where(j => j.Status == UpscaleJobStatus.Completed).ToList();
        foreach (var job in completedJobs)
        {
            _jobs.TryRemove(job.JobId, out _);
        }
        var count = await _repository.DeleteByStatusAsync(UpscaleJobStatus.Completed);
        _logger.LogInformation("Cleared {Count} completed jobs", count);
        return count;
    }

    /// <summary>
    /// Clear all jobs
    /// </summary>
    public async Task<int> ClearAllJobsAsync()
    {
        var count = _jobs.Count;
        _jobs.Clear();

        await _repository.DeleteByStatusAsync(
            UpscaleJobStatus.Pending,
            UpscaleJobStatus.Running,
            UpscaleJobStatus.Paused,
            UpscaleJobStatus.Completed,
            UpscaleJobStatus.Failed,
            UpscaleJobStatus.Cancelled);

        _logger.LogInformation("Cleared all {Count} jobs", count);
        return count;
    }

    private async Task InitializeAsync()
    {
        if (_isInitialized) return;

        try
        {
            var jobs = await _repository.GetAllAsync();
            foreach (var job in jobs)
            {
                _jobs[job.JobId] = job;

                if (job.Status == UpscaleJobStatus.Pending)
                {
                    await _taskQueue.QueueBackgroundWorkItemAsync(async token =>
                    {
                        await ProcessJobAsync(job.JobId, token);
                    });
                }
                else if (job.Status == UpscaleJobStatus.Running)
                {
                    job.Status = UpscaleJobStatus.Failed;
                    job.LastError = "Job interrupted by service restart";
                    job.CompletedAt = DateTime.UtcNow;
                    await _repository.UpdateAsync(job);
                }
            }

            _logger.LogInformation("Loaded {JobCount} jobs from database", jobs.Count());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading jobs from database");
        }

        _isInitialized = true;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("WorkerQueueService starting...");

        await InitializeAsync();

        _logger.LogInformation("WorkerQueueService started (max concurrent: {MaxConcurrent})", _maxConcurrentJobs);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var workItem = await _taskQueue.DequeueAsync(stoppingToken);
                await workItem(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in queue processing");
            }
        }

        _logger.LogInformation("WorkerQueueService stopped");
    }

    private async Task ProcessJobAsync(Guid jobId, CancellationToken cancellationToken)
    {
        if (!_jobs.TryGetValue(jobId, out var job))
        {
            _logger.LogWarning("Job {JobId} not found for processing", jobId);
            return;
        }

        // Wait if queue is paused
        while (_isQueuePaused && !cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(500, cancellationToken);
        }

        if (job.Status == UpscaleJobStatus.Cancelled)
        {
            return;
        }

        await _processingSemaphore.WaitAsync(cancellationToken);

        // Create a job-specific CancellationTokenSource linked to the service's stopping token
        using var jobCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _jobCancellations[jobId] = jobCts;
        var jobToken = jobCts.Token;

        try
        {
            job.Status = UpscaleJobStatus.Running;
            job.StartedAt = DateTime.UtcNow;
            job.ProcessId = Environment.ProcessId;
            job.MachineName = Environment.MachineName;
            await _repository.UpdateAsync(job);
            OnStatusChanged(job);

            _logger.LogInformation("Processing job {JobId} ({UpscaleType})", jobId, job.UpscaleType);

            var progress = new Progress<double>(percentage =>
            {
                job.ProgressPercentage = percentage;
                job.LastUpdatedAt = DateTime.UtcNow;

                // Calculate current frame from percentage if TotalFrames is known
                if (job.TotalFrames.HasValue && job.TotalFrames > 0)
                {
                    job.CurrentFrame = (int)(percentage / 100.0 * job.TotalFrames.Value);
                }

                OnProgressChanged(job);

                if (percentage % 10 < 1) // Log every ~10%
                {
                    _logger.LogDebug("Job {JobId} progress: {Progress:F1}%", jobId, percentage);
                }
            });

            var success = await _processor.ProcessJobAsync(job, progress, jobToken);

            if (success && job.Status == UpscaleJobStatus.Running)
            {
                job.Status = UpscaleJobStatus.Completed;
                job.CompletedAt = DateTime.UtcNow;
                job.ProgressPercentage = 100;
                await _repository.UpdateAsync(job);
                OnStatusChanged(job);
                _logger.LogInformation("Job {JobId} completed successfully", jobId);
            }
            else if (!success && job.Status == UpscaleJobStatus.Running)
            {
                job.Status = UpscaleJobStatus.Failed;
                job.LastError = "Processing failed";
                job.CompletedAt = DateTime.UtcNow;
                await _repository.UpdateAsync(job);
                OnStatusChanged(job);
                _logger.LogWarning("Job {JobId} failed", jobId);
            }
        }
        catch (OperationCanceledException)
        {
            job.Status = UpscaleJobStatus.Cancelled;
            job.CompletedAt = DateTime.UtcNow;
            await _repository.UpdateAsync(job);
            OnStatusChanged(job);
            _logger.LogInformation("Job {JobId} cancelled", jobId);
        }
        catch (Exception ex)
        {
            job.Status = UpscaleJobStatus.Failed;
            job.LastError = ex.Message;
            job.ErrorStackTrace = ex.StackTrace;
            job.CompletedAt = DateTime.UtcNow;
            await _repository.UpdateAsync(job);
            OnStatusChanged(job);
            _logger.LogError(ex, "Job {JobId} failed with error", jobId);
        }
        finally
        {
            // Clean up the job cancellation token
            _jobCancellations.TryRemove(jobId, out _);

            job.ProcessId = null;
            job.LastUpdatedAt = DateTime.UtcNow;
            _processingSemaphore.Release();

            // Auto-pause queue if no pending jobs remain
            await CheckAndAutoPauseQueueAsync();
        }
    }

    /// <summary>
    /// Automatically pause the queue if no pending or running jobs remain
    /// </summary>
    private async Task CheckAndAutoPauseQueueAsync()
    {
        var hasPendingJobs = _jobs.Values.Any(j =>
            j.Status is UpscaleJobStatus.Pending or UpscaleJobStatus.Running or UpscaleJobStatus.Paused);

        if (!hasPendingJobs && !_isQueuePaused)
        {
            _isQueuePaused = true;
            QueueStatusChanged?.Invoke(this, true);
            _logger.LogInformation("Queue auto-paused - no pending jobs remaining");
        }
    }

    private void OnProgressChanged(UpscaleJob job)
    {
        ProgressChanged?.Invoke(this, new UpscaleProgressEventArgs
        {
            JobId = job.JobId,
            Status = job.Status,
            ProgressPercentage = job.ProgressPercentage,
            CurrentFrame = job.CurrentFrame,
            TotalFrames = job.TotalFrames,
            EstimatedTimeRemaining = job.EstimatedTimeRemaining
        });
    }

    private void OnStatusChanged(UpscaleJob job)
    {
        StatusChanged?.Invoke(this, new UpscaleProgressEventArgs
        {
            JobId = job.JobId,
            Status = job.Status,
            ProgressPercentage = job.ProgressPercentage,
            CurrentFrame = job.CurrentFrame,
            TotalFrames = job.TotalFrames,
            EstimatedTimeRemaining = job.EstimatedTimeRemaining,
            ErrorMessage = job.LastError
        });
    }
}
