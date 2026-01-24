using System.Collections.Concurrent;
using System.Diagnostics;
using CheapUpscaler.Shared.Data;
using CheapUpscaler.Shared.Models;
using CheapUpscaler.Shared.Services;
using Microsoft.Extensions.Hosting;

namespace CheapUpscaler.Blazor.Services;

/// <summary>
/// Manages the upscale job queue and processes jobs in the background
/// Uses database persistence with in-memory cache for fast access
/// </summary>
public class UpscaleQueueService : BackgroundService, IUpscaleQueueService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IBackgroundTaskQueue _taskQueue;
    private readonly IUpscaleJobRepository _repository;
    private readonly IUpscaleProcessorService _processor;
    private readonly ConcurrentDictionary<Guid, UpscaleJob> _jobs = new();
    private readonly SemaphoreSlim _processingSemaphore;
    private volatile bool _isQueuePaused = true;
    private bool _isInitialized;

    public event EventHandler<UpscaleProgressEventArgs>? ProgressChanged;
    public event EventHandler<UpscaleProgressEventArgs>? StatusChanged;
    public event EventHandler<bool>? QueueStatusChanged;

    public bool IsQueuePaused => _isQueuePaused;

    public UpscaleQueueService(
        IServiceProvider serviceProvider,
        IBackgroundTaskQueue taskQueue,
        IUpscaleJobRepository repository,
        IUpscaleProcessorService processor,
        int maxConcurrentJobs = 1)
    {
        _serviceProvider = serviceProvider;
        _taskQueue = taskQueue;
        _repository = repository;
        _processor = processor;
        _processingSemaphore = new SemaphoreSlim(maxConcurrentJobs, maxConcurrentJobs);
    }

    /// <summary>
    /// Load jobs from database into memory cache
    /// </summary>
    private async Task InitializeAsync()
    {
        if (_isInitialized) return;

        try
        {
            var jobs = await _repository.GetAllAsync();
            foreach (var job in jobs)
            {
                _jobs[job.JobId] = job;

                // Re-queue pending jobs
                if (job.Status == UpscaleJobStatus.Pending)
                {
                    await _taskQueue.QueueBackgroundWorkItemAsync(async token =>
                    {
                        await ProcessJobAsync(job.JobId, token);
                    });
                }
                // Mark running jobs as failed (interrupted by shutdown)
                else if (job.Status == UpscaleJobStatus.Running)
                {
                    job.Status = UpscaleJobStatus.Failed;
                    job.LastError = "Job interrupted by application shutdown";
                    job.CompletedAt = DateTime.UtcNow;
                    await _repository.UpdateAsync(job);
                }
            }

            Debug.WriteLine($"Loaded {jobs.Count()} jobs from database");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error loading jobs from database: {ex.Message}");
        }

        _isInitialized = true;
    }

    public void StartQueue()
    {
        _isQueuePaused = false;
        QueueStatusChanged?.Invoke(this, false);
        Debug.WriteLine("Queue started");
    }

    public void StopQueue()
    {
        _isQueuePaused = true;
        QueueStatusChanged?.Invoke(this, true);
        Debug.WriteLine("Queue paused");
    }

    public async Task<Guid> AddJobAsync(UpscaleJob job)
    {
        job.QueuedAt = DateTime.UtcNow;
        job.Status = UpscaleJobStatus.Pending;

        // Persist to database
        await _repository.AddAsync(job);

        // Add to memory cache
        _jobs[job.JobId] = job;

        // Queue the processing task
        await _taskQueue.QueueBackgroundWorkItemAsync(async token =>
        {
            await ProcessJobAsync(job.JobId, token);
        });

        Debug.WriteLine($"Job {job.JobId} added to queue");
        return job.JobId;
    }

    public async Task<bool> PauseJobAsync(Guid jobId)
    {
        if (_jobs.TryGetValue(jobId, out var job) && job.Status == UpscaleJobStatus.Running)
        {
            job.Status = UpscaleJobStatus.Paused;
            job.LastUpdatedAt = DateTime.UtcNow;
            await _repository.UpdateAsync(job);
            OnStatusChanged(job);
            return true;
        }
        return false;
    }

    public async Task<bool> ResumeJobAsync(Guid jobId)
    {
        if (_jobs.TryGetValue(jobId, out var job) && job.Status == UpscaleJobStatus.Paused)
        {
            job.Status = UpscaleJobStatus.Pending;
            job.LastUpdatedAt = DateTime.UtcNow;
            await _repository.UpdateAsync(job);
            OnStatusChanged(job);
            return true;
        }
        return false;
    }

    public async Task<bool> CancelJobAsync(Guid jobId)
    {
        if (_jobs.TryGetValue(jobId, out var job) &&
            (job.Status == UpscaleJobStatus.Pending ||
             job.Status == UpscaleJobStatus.Running ||
             job.Status == UpscaleJobStatus.Paused))
        {
            job.Status = UpscaleJobStatus.Cancelled;
            job.LastUpdatedAt = DateTime.UtcNow;
            job.CompletedAt = DateTime.UtcNow;
            await _repository.UpdateAsync(job);
            OnStatusChanged(job);
            return true;
        }
        return false;
    }

    public async Task<bool> RetryJobAsync(Guid jobId)
    {
        if (_jobs.TryGetValue(jobId, out var job) &&
            (job.Status == UpscaleJobStatus.Failed || job.Status == UpscaleJobStatus.Cancelled))
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

            // Re-queue for processing
            await _taskQueue.QueueBackgroundWorkItemAsync(async token =>
            {
                await ProcessJobAsync(job.JobId, token);
            });

            OnStatusChanged(job);
            return true;
        }
        return false;
    }

    public async Task<bool> DeleteJobAsync(Guid jobId)
    {
        if (_jobs.TryRemove(jobId, out _))
        {
            await _repository.DeleteAsync(jobId);
            Debug.WriteLine($"Job {jobId} deleted");
            return true;
        }
        return false;
    }

    public Task<UpscaleJob?> GetJobAsync(Guid jobId)
    {
        _jobs.TryGetValue(jobId, out var job);
        return Task.FromResult(job);
    }

    public Task<IEnumerable<UpscaleJob>> GetAllJobsAsync()
    {
        return Task.FromResult(_jobs.Values.OrderByDescending(j => j.CreatedAt).AsEnumerable());
    }

    public Task<IEnumerable<UpscaleJob>> GetActiveJobsAsync()
    {
        var activeStatuses = new[] { UpscaleJobStatus.Pending, UpscaleJobStatus.Running, UpscaleJobStatus.Paused };
        return Task.FromResult(_jobs.Values
            .Where(j => activeStatuses.Contains(j.Status))
            .OrderByDescending(j => j.CreatedAt)
            .AsEnumerable());
    }

    public Task<IEnumerable<UpscaleJob>> GetCompletedJobsAsync()
    {
        return Task.FromResult(_jobs.Values
            .Where(j => j.Status == UpscaleJobStatus.Completed)
            .OrderByDescending(j => j.CompletedAt)
            .AsEnumerable());
    }

    public Task<IEnumerable<UpscaleJob>> GetFailedJobsAsync()
    {
        var failedStatuses = new[] { UpscaleJobStatus.Failed, UpscaleJobStatus.Cancelled };
        return Task.FromResult(_jobs.Values
            .Where(j => failedStatuses.Contains(j.Status))
            .OrderByDescending(j => j.CompletedAt)
            .AsEnumerable());
    }

    public Task<QueueStatistics> GetQueueStatisticsAsync()
    {
        var stats = new QueueStatistics
        {
            PendingCount = _jobs.Values.Count(j => j.Status == UpscaleJobStatus.Pending),
            RunningCount = _jobs.Values.Count(j => j.Status == UpscaleJobStatus.Running),
            CompletedCount = _jobs.Values.Count(j => j.Status == UpscaleJobStatus.Completed),
            FailedCount = _jobs.Values.Count(j => j.Status == UpscaleJobStatus.Failed || j.Status == UpscaleJobStatus.Cancelled)
        };
        return Task.FromResult(stats);
    }

    public async Task<int> ClearCompletedJobsAsync()
    {
        var completedJobs = _jobs.Values.Where(j => j.Status == UpscaleJobStatus.Completed).ToList();
        foreach (var job in completedJobs)
        {
            _jobs.TryRemove(job.JobId, out _);
        }
        var count = await _repository.DeleteByStatusAsync(UpscaleJobStatus.Completed);
        return count;
    }

    public async Task<int> ClearAllJobsAsync()
    {
        var count = _jobs.Count;
        _jobs.Clear();

        // Delete all from database
        await _repository.DeleteByStatusAsync(
            UpscaleJobStatus.Pending,
            UpscaleJobStatus.Running,
            UpscaleJobStatus.Paused,
            UpscaleJobStatus.Completed,
            UpscaleJobStatus.Failed,
            UpscaleJobStatus.Cancelled);

        return count;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Debug.WriteLine("UpscaleQueueService starting...");

        // Initialize from database
        await InitializeAsync();

        Debug.WriteLine("UpscaleQueueService started");

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
                Debug.WriteLine($"Error in queue processing: {ex.Message}");
            }
        }

        Debug.WriteLine("UpscaleQueueService stopped");
    }

    private async Task ProcessJobAsync(Guid jobId, CancellationToken cancellationToken)
    {
        if (!_jobs.TryGetValue(jobId, out var job))
        {
            Debug.WriteLine($"Job {jobId} not found for processing");
            return;
        }

        // Wait if queue is paused
        while (_isQueuePaused && !cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(500, cancellationToken);
        }

        // Skip if job was cancelled while waiting
        if (job.Status == UpscaleJobStatus.Cancelled)
        {
            return;
        }

        // Wait for processing slot
        await _processingSemaphore.WaitAsync(cancellationToken);

        try
        {
            job.Status = UpscaleJobStatus.Running;
            job.StartedAt = DateTime.UtcNow;
            job.ProcessId = Environment.ProcessId;
            job.MachineName = Environment.MachineName;
            await _repository.UpdateAsync(job);
            OnStatusChanged(job);

            // Create progress reporter that updates the job and fires events
            var progress = new Progress<double>(percentage =>
            {
                job.ProgressPercentage = percentage;
                job.LastUpdatedAt = DateTime.UtcNow;
                OnProgressChanged(job);
            });

            // Process using the actual upscaling service
            var success = await _processor.ProcessJobAsync(job, progress, cancellationToken);

            if (success && job.Status == UpscaleJobStatus.Running) // Wasn't cancelled during processing
            {
                job.Status = UpscaleJobStatus.Completed;
                job.CompletedAt = DateTime.UtcNow;
                job.ProgressPercentage = 100;
                await _repository.UpdateAsync(job);
                OnStatusChanged(job);
            }
            else if (!success && job.Status == UpscaleJobStatus.Running)
            {
                // Processing returned false (failure or cancellation without exception)
                job.Status = UpscaleJobStatus.Failed;
                job.LastError = "Processing failed";
                job.CompletedAt = DateTime.UtcNow;
                await _repository.UpdateAsync(job);
                OnStatusChanged(job);
            }
        }
        catch (OperationCanceledException)
        {
            job.Status = UpscaleJobStatus.Cancelled;
            job.CompletedAt = DateTime.UtcNow;
            await _repository.UpdateAsync(job);
            OnStatusChanged(job);
        }
        catch (Exception ex)
        {
            job.Status = UpscaleJobStatus.Failed;
            job.LastError = ex.Message;
            job.ErrorStackTrace = ex.StackTrace;
            job.CompletedAt = DateTime.UtcNow;
            await _repository.UpdateAsync(job);
            OnStatusChanged(job);
        }
        finally
        {
            job.ProcessId = null;
            job.LastUpdatedAt = DateTime.UtcNow;
            _processingSemaphore.Release();
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
