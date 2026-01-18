using System.Diagnostics;
using System.Text.Json;
using CheapUpscaler.Blazor.Models;
using CheapUpscaler.Core.Models;
using CheapUpscaler.Core.Services.RIFE;
using CheapUpscaler.Core.Services.RealCUGAN;
using CheapUpscaler.Core.Services.RealESRGAN;
using CheapUpscaler.Core.Services.Upscaling;
using static CheapUpscaler.Blazor.Components.Shared.AddUpscaleJobDialog;

namespace CheapUpscaler.Blazor.Services;

/// <summary>
/// Orchestrates video upscaling by mapping job settings to Core services
/// </summary>
public class UpscaleProcessorService(
    RifeInterpolationService rifeService,
    RealCuganService realCuganService,
    RealEsrganService realEsrganService,
    NonAiUpscalingService nonAiService,
    ISettingsService settingsService) : IUpscaleProcessorService
{
    public async Task<bool> ProcessJobAsync(
        UpscaleJob job,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        Debug.WriteLine($"Processing job {job.JobId} - Type: {job.UpscaleType}");

        return job.UpscaleType switch
        {
            UpscaleType.Rife => await ProcessRifeAsync(job, progress, cancellationToken),
            UpscaleType.RealCugan => await ProcessRealCuganAsync(job, progress, cancellationToken),
            UpscaleType.RealEsrgan => await ProcessRealEsrganAsync(job, progress, cancellationToken),
            UpscaleType.NonAi => await ProcessNonAiAsync(job, progress, cancellationToken),
            _ => throw new NotSupportedException($"Unsupported upscale type: {job.UpscaleType}")
        };
    }

    private async Task<bool> ProcessRifeAsync(
        UpscaleJob job,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        // Pre-validate RIFE is configured
        if (!rifeService.IsConfigured)
        {
            throw new InvalidOperationException(
                "RIFE is not configured. Install SVP 4 Pro or configure RifeFolderPath in Settings.");
        }

        var jobSettings = DeserializeSettings<RifeJobSettings>(job.SettingsJson);
        var appSettings = await settingsService.LoadAsync();

        // Map quality preset to model, with fallback to first available
        var availableModels = rifeService.GetAvailableModels();
        if (availableModels.Count == 0)
        {
            throw new InvalidOperationException(
                "No RIFE ONNX models found. Check your SVP installation or RIFE folder path.");
        }

        var preferredModel = jobSettings.QualityPreset switch
        {
            "Fast" => "rife-v4.6",
            "Medium" => "rife-v4.6",
            "High" => "rife-v4.16-lite",
            _ => "rife-v4.6"
        };

        // Use preferred model if available, otherwise fall back to first available
        var modelName = availableModels.Contains(preferredModel, StringComparer.OrdinalIgnoreCase)
            ? preferredModel
            : availableModels[0];

        if (modelName != preferredModel)
        {
            Debug.WriteLine($"RIFE: Preferred model '{preferredModel}' not available, using '{modelName}' instead");
        }

        var options = new RifeOptions
        {
            InterpolationMultiplier = jobSettings.Multiplier,
            TargetFps = jobSettings.TargetFps,
            Engine = RifeEngine.TensorRT,
            GpuId = 0,
            ModelName = modelName
        };

        Debug.WriteLine($"RIFE: {jobSettings.Multiplier}x, Target FPS: {jobSettings.TargetFps}, Model: {options.ModelName}");

        var ffmpegPath = !string.IsNullOrEmpty(appSettings.ToolPaths.FFmpegPath)
            ? appSettings.ToolPaths.FFmpegPath
            : null;

        return await rifeService.InterpolateVideoAsync(
            job.SourceVideoPath,
            job.OutputPath,
            options,
            progress,
            cancellationToken,
            ffmpegPath);
    }

    private async Task<bool> ProcessRealCuganAsync(
        UpscaleJob job,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        var jobSettings = DeserializeSettings<RealCuganJobSettings>(job.SettingsJson);
        var appSettings = await settingsService.LoadAsync();

        var options = new RealCuganOptions
        {
            Noise = jobSettings.NoiseLevel,
            Scale = jobSettings.Scale,
            UseFp16 = jobSettings.UseFp16,
            GpuId = 0,
            NumStreams = 1,
            Backend = 0 // TensorRT
        };

        // Validate noise/scale compatibility
        if (!options.IsNoiseScaleCompatible())
        {
            throw new InvalidOperationException(
                $"Noise level {options.Noise} is not compatible with scale {options.Scale}. " +
                "Noise levels 1 and 2 only work with 2x scale.");
        }

        Debug.WriteLine($"RealCUGAN: Scale {options.Scale}x, Noise: {options.Noise}, FP16: {options.UseFp16}");

        var ffmpegPath = !string.IsNullOrEmpty(appSettings.ToolPaths.FFmpegPath)
            ? appSettings.ToolPaths.FFmpegPath
            : null;

        return await realCuganService.UpscaleVideoAsync(
            job.SourceVideoPath,
            job.OutputPath,
            options,
            progress,
            cancellationToken,
            ffmpegPath);
    }

    private async Task<bool> ProcessRealEsrganAsync(
        UpscaleJob job,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        var jobSettings = DeserializeSettings<RealEsrganJobSettings>(job.SettingsJson);
        var appSettings = await settingsService.LoadAsync();

        var options = new RealEsrganOptions
        {
            ModelName = jobSettings.Model,
            ScaleFactor = jobSettings.Scale,
            TileMode = true,
            TileSize = jobSettings.TileSize,
            TilePad = 10,
            UseFp16 = jobSettings.UseFp16,
            GpuId = 0,
            NumThreads = 1
        };

        Debug.WriteLine($"RealESRGAN: Model {options.ModelName}, Scale {options.ScaleFactor}x, Tile: {options.TileSize}, FP16: {options.UseFp16}");

        var ffmpegPath = !string.IsNullOrEmpty(appSettings.ToolPaths.FFmpegPath)
            ? appSettings.ToolPaths.FFmpegPath
            : null;

        return await realEsrganService.UpscaleVideoAsync(
            job.SourceVideoPath,
            job.OutputPath,
            options,
            progress,
            cancellationToken,
            ffmpegPath);
    }

    private async Task<bool> ProcessNonAiAsync(
        UpscaleJob job,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        var jobSettings = DeserializeSettings<NonAiJobSettings>(job.SettingsJson);

        // Map algorithm name to lowercase for the service
        var algorithm = jobSettings.Algorithm.ToLowerInvariant();

        Debug.WriteLine($"NonAI: Algorithm {algorithm}, Scale {jobSettings.Scale}x");

        return await nonAiService.UpscaleVideoAsync(
            job.SourceVideoPath,
            job.OutputPath,
            algorithm,
            jobSettings.Scale,
            progress,
            cancellationToken);
    }

    private static T DeserializeSettings<T>(string json) where T : new()
    {
        if (string.IsNullOrEmpty(json) || json == "{}")
        {
            return new T();
        }

        try
        {
            return JsonSerializer.Deserialize<T>(json) ?? new T();
        }
        catch (JsonException ex)
        {
            Debug.WriteLine($"Failed to deserialize settings: {ex.Message}");
            return new T();
        }
    }
}
