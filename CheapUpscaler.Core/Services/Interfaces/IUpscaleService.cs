using CheapUpscaler.Core.Models;

namespace CheapUpscaler.Core.Services.Interfaces;

/// <summary>
/// Video/image upscaling abstraction
/// Supports Real-ESRGAN, Real-CUGAN, and classic (non-AI) methods
/// </summary>
public interface IUpscaleService
{
    /// <summary>
    /// Check if Real-ESRGAN is available
    /// </summary>
    Task<bool> IsRealEsrganAvailableAsync();

    /// <summary>
    /// Check if Real-CUGAN is available
    /// </summary>
    Task<bool> IsRealCuganAvailableAsync();

    /// <summary>
    /// Check if TensorRT acceleration is available for upscaling
    /// </summary>
    Task<bool> IsTensorRTAvailableAsync();

    /// <summary>
    /// Upscale video using Real-ESRGAN
    /// </summary>
    Task<bool> UpscaleWithRealEsrganAsync(
        string inputPath,
        string outputPath,
        RealEsrganOptions settings,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Upscale video using Real-CUGAN (optimized for anime)
    /// </summary>
    Task<bool> UpscaleWithRealCuganAsync(
        string inputPath,
        string outputPath,
        RealCuganOptions settings,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Upscale using non-AI methods (xBR, Lanczos, HQx)
    /// </summary>
    Task<bool> UpscaleWithClassicAsync(
        string inputPath,
        string outputPath,
        NonAiUpscalingOptions settings,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get available upscaling models for Real-ESRGAN
    /// </summary>
    IReadOnlyList<string> GetRealEsrganModels();

    /// <summary>
    /// Get available upscaling models for Real-CUGAN
    /// </summary>
    IReadOnlyList<string> GetRealCuganModels();

    /// <summary>
    /// Get available classic upscaling algorithms
    /// </summary>
    IReadOnlyList<string> GetClassicAlgorithms();
}
