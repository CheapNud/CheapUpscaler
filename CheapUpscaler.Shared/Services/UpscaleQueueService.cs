using System.Collections.Concurrent;
using CheapUpscaler.Shared.Data;
using CheapUpscaler.Shared.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CheapUpscaler.Shared.Services;

/// <summary>
/// The queue engine for both hosts: persists jobs, dispatches them to the host's
/// <see cref="IUpscaleProcessor"/> and raises progress/status events for the UI.
/// </summary>
public class UpscaleQueueService(
    int maxConcurrentJobs,
    bool autoPauseWhenIdle,
    IUpscaleProcessor processor,
    IUpscaleJobRepository repository,
    IBackgroundTaskQueue taskQueue,
    ILogger<UpscaleQueueService> logger) : BackgroundService, IUpscaleQueueService
{
    private const int PausePollMilliseconds = 500;

    private readonly ConcurrentDictionary<Guid, UpscaleJob> _jobs = new();
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _jobCancellations = new();
    private readonly SemaphoreSlim _processingSemaphore = new(maxConcurrentJobs, maxConcurrentJobs);
    private volatile bool _isQueuePaused = true; // Boot paused by design - the user starts the queue
    private bool _isInitialized;

    public event EventHandler<UpscaleProgressEventArgs>? ProgressChanged;
    public event EventHandler<UpscaleProgressEventArgs>? StatusChanged;
    public event EventHandler<bool>? QueueStatusChanged;

    public bool IsQueuePaused => _isQueuePaused;

    public void StartQueue()
    {
        _isQueuePaused = false;
        QueueStatusChanged?.Invoke(this, false);
        logger.LogInformation("Queue started");
    }

    public void StopQueue()
    {
        _isQueuePaused = true;
        QueueStatusChanged?.Invoke(this, true);
        logger.LogInformation("Queue paused");
    }

    /// <summary>
    /// Add a new job to the queue
    /// </summary>
    public async Task<Guid> AddJobAsync(UpscaleJob job, CancellationToken cancellationToken = default)
    {
        job.QueuedAt = DateTime.UtcNow;
        job.Status = UpscaleJobStatus.Pending;

        await repository.AddAsync(job);
        _jobs[job.JobId] = job;

        await EnqueueAsync(job.JobId, cancellationToken);

        logger.LogInformation("Job {JobId} added to queue", job.JobId);
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
    /// Cancel a job. The only way to stop a job that is already running.
    /// </summary>
    public async Task<bool> CancelJobAsync(Guid jobId)
    {
        if (!_jobs.TryGetValue(jobId, out var job) ||
            job.Status is not (UpscaleJobStatus.Pending or UpscaleJobStatus.Running or UpscaleJobStatus.Paused))
        {
            return false;
        }

        // Signal the running process, if any. The processing task writes the terminal status itself.
        if (_jobCancellations.TryRemove(jobId, out var cts))
        {
            try
            {
                await cts.CancelAsync();
                logger.LogDebug("Cancellation token triggered for job {JobId}", jobId);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Error cancelling job {JobId} token", jobId);
            }
            finally
            {
                cts.Dispose();
            }
        }

        job.Status = UpscaleJobStatus.Cancelled;
        job.LastUpdatedAt = DateTime.UtcNow;
        job.CompletedAt = DateTime.UtcNow;
        await repository.UpdateAsync(job);
        OnStatusChanged(job);
        logger.LogInformation("Job {JobId} cancelled", jobId);
        return true;
    }

    /// <summary>
    /// Pause a Pending job so the consumer skips it when its turn comes.
    /// A Running job cannot be paused - the external upscaler process has no pause - so this
    /// returns false for anything that is not Pending. Use <see cref="CancelJobAsync"/> instead.
    /// </summary>
    public async Task<bool> PauseJobAsync(Guid jobId)
    {
        if (!_jobs.TryGetValue(jobId, out var job) || job.Status != UpscaleJobStatus.Pending)
        {
            return false;
        }

        job.Status = UpscaleJobStatus.Paused;
        job.LastUpdatedAt = DateTime.UtcNow;
        await repository.UpdateAsync(job);
        OnStatusChanged(job);
        logger.LogInformation("Job {JobId} paused", jobId);
        return true;
    }

    /// <summary>
    /// Return a paused job to Pending and re-queue it (its original work item was skipped).
    /// </summary>
    public async Task<bool> ResumeJobAsync(Guid jobId)
    {
        if (!_jobs.TryGetValue(jobId, out var job) || job.Status != UpscaleJobStatus.Paused)
        {
            return false;
        }

        job.Status = UpscaleJobStatus.Pending;
        job.LastUpdatedAt = DateTime.UtcNow;
        await repository.UpdateAsync(job);
        await EnqueueAsync(jobId);
        OnStatusChanged(job);
        logger.LogInformation("Job {JobId} resumed", jobId);
        return true;
    }

    /// <summary>
    /// Retry a failed job
    /// </summary>
    public async Task<bool> RetryJobAsync(Guid jobId)
    {
        if (!_jobs.TryGetValue(jobId, out var job) ||
            job.Status is not (UpscaleJobStatus.Failed or UpscaleJobStatus.Cancelled))
        {
            return false;
        }

        job.Status = UpscaleJobStatus.Pending;
        job.ProgressPercentage = 0;
        job.CurrentFrame = 0;
        job.LastError = null;
        job.ErrorStackTrace = null;
        job.RetryCount++;
        job.LastUpdatedAt = DateTime.UtcNow;
        job.CompletedAt = null;

        await repository.UpdateAsync(job);
        await EnqueueAsync(jobId);
        OnStatusChanged(job);

        logger.LogInformation("Job {JobId} queued for retry (attempt {RetryCount})", jobId, job.RetryCount);
        return true;
    }

    /// <summary>
    /// Delete a job
    /// </summary>
    public async Task<bool> DeleteJobAsync(Guid jobId)
    {
        if (!_jobs.TryRemove(jobId, out _))
        {
            return false;
        }

        await repository.DeleteAsync(jobId);
        logger.LogInformation("Job {JobId} deleted", jobId);
        return true;
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
        var count = await repository.DeleteByStatusAsync(UpscaleJobStatus.Completed);
        logger.LogInformation("Cleared {Count} completed jobs", count);
        return count;
    }

    /// <summary>
    /// Clear all jobs
    /// </summary>
    public async Task<int> ClearAllJobsAsync()
    {
        var count = _jobs.Count;
        _jobs.Clear();

        await repository.DeleteByStatusAsync(
            UpscaleJobStatus.Pending,
            UpscaleJobStatus.Running,
            UpscaleJobStatus.Paused,
            UpscaleJobStatus.Completed,
            UpscaleJobStatus.Failed,
            UpscaleJobStatus.Cancelled);

        logger.LogInformation("Cleared all {Count} jobs", count);
        return count;
    }

    private ValueTask EnqueueAsync(Guid jobId, CancellationToken cancellationToken = default) =>
        taskQueue.QueueBackgroundWorkItemAsync(async token => await ProcessJobAsync(jobId, token), cancellationToken);

    private async Task InitializeAsync()
    {
        if (_isInitialized) return;

        try
        {
            var jobs = await repository.GetAllAsync();
            foreach (var job in jobs)
            {
                _jobs[job.JobId] = job;

                if (job.Status == UpscaleJobStatus.Pending)
                {
                    await EnqueueAsync(job.JobId);
                }
                else if (job.Status == UpscaleJobStatus.Running)
                {
                    job.Status = UpscaleJobStatus.Failed;
                    job.LastError = "Job interrupted by service restart";
                    job.CompletedAt = DateTime.UtcNow;
                    await repository.UpdateAsync(job);
                }
            }

            logger.LogInformation("Loaded {JobCount} jobs from database", jobs.Count());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error loading jobs from database");
        }

        _isInitialized = true;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("UpscaleQueueService starting...");

        await InitializeAsync();

        logger.LogInformation("UpscaleQueueService started (max concurrent: {MaxConcurrent})", maxConcurrentJobs);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Pause gate sits BEFORE the dequeue: a paused queue simply stops consuming
                // instead of holding a dequeued job hostage.
                while (_isQueuePaused && !stoppingToken.IsCancellationRequested)
                {
                    await Task.Delay(PausePollMilliseconds, stoppingToken);
                }

                // Take the slot before dequeuing, otherwise the loop would pull every work item
                // and run them one at a time regardless of maxConcurrentJobs.
                await _processingSemaphore.WaitAsync(stoppingToken);

                Func<CancellationToken, ValueTask> workItem;
                try
                {
                    workItem = await taskQueue.DequeueAsync(stoppingToken);
                }
                catch
                {
                    _processingSemaphore.Release();
                    throw;
                }

                _ = Task.Run(async () =>
                {
                    try
                    {
                        await workItem(stoppingToken);
                    }
                    catch (OperationCanceledException)
                    {
                        // Expected during shutdown
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Unhandled error in queue work item");
                    }
                    finally
                    {
                        _processingSemaphore.Release();
                    }
                }, CancellationToken.None);
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in queue processing");
            }
        }

        logger.LogInformation("UpscaleQueueService stopped");
    }

    private async Task ProcessJobAsync(Guid jobId, CancellationToken cancellationToken)
    {
        if (!_jobs.TryGetValue(jobId, out var job))
        {
            logger.LogWarning("Job {JobId} not found for processing", jobId);
            return;
        }

        // Paused/cancelled while waiting in the channel: drop the work item.
        // ResumeJobAsync re-queues a fresh one.
        if (job.Status is UpscaleJobStatus.Paused or UpscaleJobStatus.Cancelled)
        {
            logger.LogDebug("Skipping job {JobId} in status {Status}", jobId, job.Status);
            return;
        }

        // Job-specific token, linked to the service's stopping token, so CancelJobAsync
        // can kill this job without touching the rest of the queue.
        using var jobCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _jobCancellations[jobId] = jobCts;
        var jobToken = jobCts.Token;

        try
        {
            job.Status = UpscaleJobStatus.Running;
            job.StartedAt = DateTime.UtcNow;
            job.ProcessId = Environment.ProcessId;
            job.MachineName = Environment.MachineName;
            await repository.UpdateAsync(job);
            OnStatusChanged(job);

            logger.LogInformation("Processing job {JobId} ({UpscaleType})", jobId, job.UpscaleType);

            var progress = new Progress<double>(percentage =>
            {
                job.ProgressPercentage = percentage;
                job.LastUpdatedAt = DateTime.UtcNow;

                // Derive current frame from percentage when the source frame count is known
                if (job.TotalFrames is > 0)
                {
                    job.CurrentFrame = (int)(percentage / 100.0 * job.TotalFrames.Value);
                }

                OnProgressChanged(job);

                if (percentage % 10 < 1) // Log every ~10%
                {
                    logger.LogDebug("Job {JobId} progress: {Progress:F1}%", jobId, percentage);
                }
            });

            var success = await processor.ProcessJobAsync(job, progress, jobToken);

            // A started job always gets a terminal status written - no status guard here,
            // that is what used to leave jobs stuck as Running forever.
            if (jobToken.IsCancellationRequested)
            {
                job.Status = UpscaleJobStatus.Cancelled;
                job.CompletedAt = DateTime.UtcNow;
                await repository.UpdateAsync(job);
                OnStatusChanged(job);
                logger.LogInformation("Job {JobId} cancelled", jobId);
            }
            else if (success)
            {
                job.Status = UpscaleJobStatus.Completed;
                job.CompletedAt = DateTime.UtcNow;
                job.ProgressPercentage = 100;
                await repository.UpdateAsync(job);
                OnStatusChanged(job);
                logger.LogInformation("Job {JobId} completed successfully", jobId);
            }
            else
            {
                job.Status = UpscaleJobStatus.Failed;
                job.LastError = "Processing failed";
                job.CompletedAt = DateTime.UtcNow;
                await repository.UpdateAsync(job);
                OnStatusChanged(job);
                logger.LogWarning("Job {JobId} failed", jobId);
            }
        }
        catch (OperationCanceledException)
        {
            job.Status = UpscaleJobStatus.Cancelled;
            job.CompletedAt = DateTime.UtcNow;
            await repository.UpdateAsync(job);
            OnStatusChanged(job);
            logger.LogInformation("Job {JobId} cancelled", jobId);
        }
        catch (Exception ex)
        {
            job.Status = UpscaleJobStatus.Failed;
            job.LastError = ex.Message;
            job.ErrorStackTrace = ex.StackTrace;
            job.CompletedAt = DateTime.UtcNow;
            await repository.UpdateAsync(job);
            OnStatusChanged(job);
            logger.LogError(ex, "Job {JobId} failed with error", jobId);
        }
        finally
        {
            _jobCancellations.TryRemove(jobId, out _);

            job.ProcessId = null;
            job.LastUpdatedAt = DateTime.UtcNow;

            CheckAndAutoPauseQueue();
        }
    }

    /// <summary>
    /// Automatically pause the queue if no pending or running jobs remain.
    /// Opt-in per host - off by default so a running queue stays running.
    /// </summary>
    private void CheckAndAutoPauseQueue()
    {
        if (!autoPauseWhenIdle)
        {
            return;
        }

        var hasPendingJobs = _jobs.Values.Any(j =>
            j.Status is UpscaleJobStatus.Pending or UpscaleJobStatus.Running or UpscaleJobStatus.Paused);

        if (!hasPendingJobs && !_isQueuePaused)
        {
            _isQueuePaused = true;
            QueueStatusChanged?.Invoke(this, true);
            logger.LogInformation("Queue auto-paused - no pending jobs remaining");
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
