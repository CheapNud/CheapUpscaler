using CheapUpscaler.Shared.Models;

namespace CheapUpscaler.Worker.Services;

/// <summary>
/// Worker-specific upscale processor service
/// </summary>
public interface IWorkerProcessorService
{
    /// <summary>
    /// Process an upscale job
    /// </summary>
    Task<bool> ProcessJobAsync(
        UpscaleJob job,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default);
}
