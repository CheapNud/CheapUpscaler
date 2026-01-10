using System.Collections.Concurrent;
using System.Diagnostics;
using CheapUpscaler.Blazor.Models;
using Microsoft.Extensions.Hosting;

namespace CheapUpscaler.Blazor.Services;

/// <summary>
/// Manages the upscale job queue and processes jobs in the background
/// In-memory implementation for initial development - can be extended with database persistence
/// </summary>
public class UpscaleQueueService : BackgroundService, IUpscaleQueueService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IBackgroundTaskQueue _taskQueue;
    private readonly ConcurrentDictionary<Guid, UpscaleJob> _jobs = new();
    private readonly SemaphoreSlim _processingSemaphore;
    private volatile bool _isQueuePaused = true;

    public event EventHandler<UpscaleProgressEventArgs>? ProgressChanged;
    public event EventHandler<UpscaleProgressEventArgs>? StatusChanged;
    public event EventHandler<bool>? QueueStatusChanged;

    public bool IsQueuePaused => _isQueuePaused;

    public UpscaleQueueService(
        IServiceProvider serviceProvider,
        IBackgroundTaskQueue taskQueue,
        int maxConcurrentJobs = 1)
    {
        _serviceProvider = serviceProvider;
        _taskQueue = taskQueue;
        _processingSemaphore = new SemaphoreSlim(maxConcurrentJobs, maxConcurrentJobs);
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
        _jobs[job.JobId] = job;

        // Queue the processing task
        await _taskQueue.QueueBackgroundWorkItemAsync(async token =>
        {
            await ProcessJobAsync(job.JobId, token);
        });

        Debug.WriteLine($"Job {job.JobId} added to queue");
        return job.JobId;
    }

    public Task<bool> PauseJobAsync(Guid jobId)
    {
        if (_jobs.TryGetValue(jobId, out var job) && job.Status == UpscaleJobStatus.Running)
        {
            job.Status = UpscaleJobStatus.Paused;
            job.LastUpdatedAt = DateTime.UtcNow;
            OnStatusChanged(job);
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    public Task<bool> ResumeJobAsync(Guid jobId)
    {
        if (_jobs.TryGetValue(jobId, out var job) && job.Status == UpscaleJobStatus.Paused)
        {
            job.Status = UpscaleJobStatus.Pending;
            job.LastUpdatedAt = DateTime.UtcNow;
            OnStatusChanged(job);
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    public Task<bool> CancelJobAsync(Guid jobId)
    {
        if (_jobs.TryGetValue(jobId, out var job) &&
            (job.Status == UpscaleJobStatus.Pending ||
             job.Status == UpscaleJobStatus.Running ||
             job.Status == UpscaleJobStatus.Paused))
        {
            job.Status = UpscaleJobStatus.Cancelled;
            job.LastUpdatedAt = DateTime.UtcNow;
            job.CompletedAt = DateTime.UtcNow;
            OnStatusChanged(job);
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    public Task<bool> RetryJobAsync(Guid jobId)
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

            // Re-queue for processing
            _ = _taskQueue.QueueBackgroundWorkItemAsync(async token =>
            {
                await ProcessJobAsync(job.JobId, token);
            });

            OnStatusChanged(job);
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    public Task<bool> DeleteJobAsync(Guid jobId)
    {
        if (_jobs.TryRemove(jobId, out _))
        {
            Debug.WriteLine($"Job {jobId} deleted");
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
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

    public Task<int> ClearCompletedJobsAsync()
    {
        var completedJobs = _jobs.Values.Where(j => j.Status == UpscaleJobStatus.Completed).ToList();
        foreach (var job in completedJobs)
        {
            _jobs.TryRemove(job.JobId, out _);
        }
        return Task.FromResult(completedJobs.Count);
    }

    public Task<int> ClearAllJobsAsync()
    {
        var count = _jobs.Count;
        _jobs.Clear();
        return Task.FromResult(count);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
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
            OnStatusChanged(job);

            // TODO: Integrate with actual upscaling services based on job.UpscaleType
            // For now, simulate processing
            await SimulateProcessingAsync(job, cancellationToken);

            if (job.Status == UpscaleJobStatus.Running) // Wasn't cancelled during processing
            {
                job.Status = UpscaleJobStatus.Completed;
                job.CompletedAt = DateTime.UtcNow;
                job.ProgressPercentage = 100;
                OnStatusChanged(job);
            }
        }
        catch (OperationCanceledException)
        {
            job.Status = UpscaleJobStatus.Cancelled;
            job.CompletedAt = DateTime.UtcNow;
            OnStatusChanged(job);
        }
        catch (Exception ex)
        {
            job.Status = UpscaleJobStatus.Failed;
            job.LastError = ex.Message;
            job.ErrorStackTrace = ex.StackTrace;
            job.CompletedAt = DateTime.UtcNow;
            OnStatusChanged(job);
        }
        finally
        {
            job.ProcessId = null;
            job.LastUpdatedAt = DateTime.UtcNow;
            _processingSemaphore.Release();
        }
    }

    /// <summary>
    /// Simulate processing for testing (replace with actual upscaling integration)
    /// </summary>
    private async Task SimulateProcessingAsync(UpscaleJob job, CancellationToken cancellationToken)
    {
        job.TotalFrames = 1000; // Simulated
        var random = new Random();

        for (int frame = 0; frame <= job.TotalFrames; frame += random.Next(5, 20))
        {
            if (cancellationToken.IsCancellationRequested || job.Status == UpscaleJobStatus.Cancelled)
                break;

            // Wait while paused
            while (job.Status == UpscaleJobStatus.Paused && !cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(500, cancellationToken);
            }

            job.CurrentFrame = Math.Min(frame, job.TotalFrames.Value);
            job.ProgressPercentage = (job.CurrentFrame / (double)job.TotalFrames.Value) * 100;
            job.EstimatedTimeRemaining = TimeSpan.FromSeconds((job.TotalFrames.Value - job.CurrentFrame) * 0.1);
            job.LastUpdatedAt = DateTime.UtcNow;

            OnProgressChanged(job);

            await Task.Delay(100, cancellationToken); // Simulate processing time
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
