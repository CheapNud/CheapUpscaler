using CheapUpscaler.Shared.Models;

namespace CheapUpscaler.Shared.Services;

/// <summary>
/// Executes an upscale job with the appropriate Core service.
/// Implemented per host (desktop uses AppSettings tool paths, worker uses IToolLocator).
/// </summary>
public interface IUpscaleProcessor
{
    /// <summary>
    /// Process an upscale job
    /// </summary>
    /// <param name="job">The upscale job to process</param>
    /// <param name="progress">Progress reporter (0-100 percentage)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if successful, false if failed or cancelled</returns>
    Task<bool> ProcessJobAsync(
        UpscaleJob job,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default);
}
