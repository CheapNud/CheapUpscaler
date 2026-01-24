namespace CheapUpscaler.Shared.Models;

/// <summary>
/// Status of an upscale job in the queue
/// </summary>
public enum UpscaleJobStatus
{
    /// <summary>Job is queued and waiting to start</summary>
    Pending,

    /// <summary>Job is currently being processed</summary>
    Running,

    /// <summary>Job was paused by user</summary>
    Paused,

    /// <summary>Job completed successfully</summary>
    Completed,

    /// <summary>Job failed with an error</summary>
    Failed,

    /// <summary>Job was cancelled by user</summary>
    Cancelled
}
