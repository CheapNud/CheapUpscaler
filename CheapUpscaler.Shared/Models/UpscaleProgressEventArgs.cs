namespace CheapUpscaler.Shared.Models;

/// <summary>
/// Event args for upscale job progress updates
/// </summary>
public class UpscaleProgressEventArgs : EventArgs
{
    public required Guid JobId { get; init; }
    public UpscaleJobStatus Status { get; init; }
    public double ProgressPercentage { get; init; }
    public int CurrentFrame { get; init; }
    public int? TotalFrames { get; init; }
    public TimeSpan? EstimatedTimeRemaining { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Queue statistics for display
/// </summary>
public class QueueStatistics
{
    public int PendingCount { get; init; }
    public int RunningCount { get; init; }
    public int CompletedCount { get; init; }
    public int FailedCount { get; init; }
    public int TotalCount => PendingCount + RunningCount + CompletedCount + FailedCount;
}
