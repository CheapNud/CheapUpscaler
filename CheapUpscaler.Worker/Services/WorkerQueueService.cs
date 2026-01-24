using System.Collections.Concurrent;
using CheapUpscaler.Shared.Data;
using CheapUpscaler.Shared.Models;
using CheapUpscaler.Shared.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CheapUpscaler.Worker.Services;

/// <summary>
/// Headless queue service for processing upscale jobs
/// Simplified from Blazor's UpscaleQueueService - no UI events
/// </summary>
public class WorkerQueueService : BackgroundService
{
    private readonly IBackgroundTaskQueue _taskQueue;
    private readonly IUpscaleJobRepository _repository;
    private readonly IWorkerProcessorService _processor;
    private readonly ILogger<WorkerQueueService> _logger;
    private readonly ConcurrentDictionary<Guid, UpscaleJob> _jobs = new();
    private readonly SemaphoreSlim _processingSemaphore;
    private readonly int _maxConcurrentJobs;
    private bool _isInitialized;

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
            job.Status = UpscaleJobStatus.Cancelled;
            job.LastUpdatedAt = DateTime.UtcNow;
            job.CompletedAt = DateTime.UtcNow;
            await _repository.UpdateAsync(job);
            _logger.LogInformation("Job {JobId} cancelled", jobId);
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

        if (job.Status == UpscaleJobStatus.Cancelled)
        {
            return;
        }

        await _processingSemaphore.WaitAsync(cancellationToken);

        try
        {
            job.Status = UpscaleJobStatus.Running;
            job.StartedAt = DateTime.UtcNow;
            job.ProcessId = Environment.ProcessId;
            job.MachineName = Environment.MachineName;
            await _repository.UpdateAsync(job);

            _logger.LogInformation("Processing job {JobId} ({UpscaleType})", jobId, job.UpscaleType);

            var progress = new Progress<double>(percentage =>
            {
                job.ProgressPercentage = percentage;
                job.LastUpdatedAt = DateTime.UtcNow;

                if (percentage % 10 < 1) // Log every ~10%
                {
                    _logger.LogDebug("Job {JobId} progress: {Progress:F1}%", jobId, percentage);
                }
            });

            var success = await _processor.ProcessJobAsync(job, progress, cancellationToken);

            if (success && job.Status == UpscaleJobStatus.Running)
            {
                job.Status = UpscaleJobStatus.Completed;
                job.CompletedAt = DateTime.UtcNow;
                job.ProgressPercentage = 100;
                await _repository.UpdateAsync(job);
                _logger.LogInformation("Job {JobId} completed successfully", jobId);
            }
            else if (!success && job.Status == UpscaleJobStatus.Running)
            {
                job.Status = UpscaleJobStatus.Failed;
                job.LastError = "Processing failed";
                job.CompletedAt = DateTime.UtcNow;
                await _repository.UpdateAsync(job);
                _logger.LogWarning("Job {JobId} failed", jobId);
            }
        }
        catch (OperationCanceledException)
        {
            job.Status = UpscaleJobStatus.Cancelled;
            job.CompletedAt = DateTime.UtcNow;
            await _repository.UpdateAsync(job);
            _logger.LogInformation("Job {JobId} cancelled", jobId);
        }
        catch (Exception ex)
        {
            job.Status = UpscaleJobStatus.Failed;
            job.LastError = ex.Message;
            job.ErrorStackTrace = ex.StackTrace;
            job.CompletedAt = DateTime.UtcNow;
            await _repository.UpdateAsync(job);
            _logger.LogError(ex, "Job {JobId} failed with error", jobId);
        }
        finally
        {
            job.ProcessId = null;
            job.LastUpdatedAt = DateTime.UtcNow;
            _processingSemaphore.Release();
        }
    }
}
