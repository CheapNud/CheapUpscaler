using CheapUpscaler.Blazor.Models;

namespace CheapUpscaler.Blazor.Services;

/// <summary>
/// Orchestrates video upscaling using CheapUpscaler.Core services
/// Maps job settings to Core service options and handles processing
/// </summary>
public interface IUpscaleProcessorService
{
    /// <summary>
    /// Process an upscale job using the appropriate Core service
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
