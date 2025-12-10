using CheapUpscaler.Core.Models;

namespace CheapUpscaler.Core.Services.Interfaces;

/// <summary>
/// RIFE frame interpolation abstraction
/// Windows: Uses VapourSynth + SVP 4 Pro
/// Linux: Uses Practical-RIFE with TensorRT
/// </summary>
public interface IRifeService
{
    /// <summary>
    /// Check if RIFE is available on this platform
    /// </summary>
    Task<bool> IsAvailableAsync();

    /// <summary>
    /// Check if TensorRT acceleration is available
    /// </summary>
    Task<bool> IsTensorRTAvailableAsync();

    /// <summary>
    /// Check if a specific RIFE model's TensorRT engine is cached
    /// </summary>
    Task<bool> IsModelCachedAsync(string modelName);

    /// <summary>
    /// Build TensorRT engine for a model (one-time operation, can take several minutes)
    /// </summary>
    Task BuildTensorRTEngineAsync(
        string modelName,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Perform frame interpolation on video
    /// </summary>
    Task<bool> InterpolateVideoAsync(
        string inputPath,
        string outputPath,
        RifeSettings settings,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Perform frame interpolation on extracted frames
    /// </summary>
    Task<bool> InterpolateFramesAsync(
        string inputFramesFolder,
        string outputFramesFolder,
        RifeSettings settings,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get list of available RIFE models
    /// </summary>
    IReadOnlyList<string> GetAvailableModels();

    /// <summary>
    /// Get recommended model for the platform
    /// </summary>
    string GetRecommendedModel();
}
