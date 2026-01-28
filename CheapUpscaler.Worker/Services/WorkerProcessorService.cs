using System.Text.Json;
using CheapUpscaler.Core.Models;
using CheapUpscaler.Core.Services.RIFE;
using CheapUpscaler.Core.Services.RealCUGAN;
using CheapUpscaler.Core.Services.RealESRGAN;
using CheapUpscaler.Core.Services.Upscaling;
using CheapUpscaler.Shared.Models;
using CheapUpscaler.Shared.Platform;
using Microsoft.Extensions.Logging;

namespace CheapUpscaler.Worker.Services;

/// <summary>
/// Worker-specific implementation of upscale processing
/// Uses Core services with platform-agnostic tool detection
/// </summary>
public class WorkerProcessorService(
    RifeInterpolationService rifeService,
    RealCuganService realCuganService,
    RealEsrganService realEsrganService,
    NonAiUpscalingService nonAiService,
    IToolLocator toolLocator,
    IConfiguration configuration,
    ILogger<WorkerProcessorService> logger) : IWorkerProcessorService
{
    public async Task<bool> ProcessJobAsync(
        UpscaleJob job,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Processing job {JobId} - Type: {UpscaleType}", job.JobId, job.UpscaleType);

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
        if (!rifeService.IsConfigured)
        {
            throw new InvalidOperationException(
                "RIFE is not configured. Ensure RIFE models are available in the configured path.");
        }

        var jobSettings = DeserializeSettings<RifeJobSettings>(job.SettingsJson);

        var availableModels = rifeService.GetAvailableModels();
        if (availableModels.Count == 0)
        {
            throw new InvalidOperationException("No RIFE models found.");
        }

        var preferredModel = jobSettings.QualityPreset switch
        {
            "Fast" => "rife-v4.6",
            "Medium" => "rife-v4.6",
            "High" => "rife-v4.16-lite",
            _ => "rife-v4.6"
        };

        var modelName = availableModels.Contains(preferredModel, StringComparer.OrdinalIgnoreCase)
            ? preferredModel
            : availableModels[0];

        var selectedEngine = rifeService.AutoSelectEngine();

        var options = new RifeOptions
        {
            InterpolationMultiplier = jobSettings.Multiplier,
            TargetFps = jobSettings.TargetFps.HasValue ? (int)jobSettings.TargetFps.Value : 60,
            Engine = selectedEngine,
            GpuId = 0,
            ModelName = modelName
        };

        logger.LogInformation("RIFE: {Multiplier}x, Target FPS: {TargetFps}, Model: {ModelName}, Engine: {Engine}",
            jobSettings.Multiplier, jobSettings.TargetFps, options.ModelName, selectedEngine);

        var ffmpegInfo = await toolLocator.DetectFFmpegAsync(cancellationToken);
        var ffmpegPath = ffmpegInfo?.Path ?? configuration["Tools:FFmpegPath"];

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

        var options = new RealCuganOptions
        {
            Noise = jobSettings.NoiseLevel,
            Scale = jobSettings.Scale,
            UseFp16 = jobSettings.UseFp16,
            GpuId = 0,
            NumStreams = 1,
            Backend = 0 // TensorRT
        };

        if (!options.IsNoiseScaleCompatible())
        {
            throw new InvalidOperationException(
                $"Noise level {options.Noise} is not compatible with scale {options.Scale}.");
        }

        logger.LogInformation("RealCUGAN: Scale {Scale}x, Noise: {Noise}, FP16: {UseFp16}",
            options.Scale, options.Noise, options.UseFp16);

        var ffmpegInfo = await toolLocator.DetectFFmpegAsync(cancellationToken);
        var ffmpegPath = ffmpegInfo?.Path ?? configuration["Tools:FFmpegPath"];

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

        logger.LogInformation("RealESRGAN: Model {ModelName}, Scale {Scale}x, Tile: {TileSize}, FP16: {UseFp16}",
            options.ModelName, options.ScaleFactor, options.TileSize, options.UseFp16);

        var ffmpegInfo = await toolLocator.DetectFFmpegAsync(cancellationToken);
        var ffmpegPath = ffmpegInfo?.Path ?? configuration["Tools:FFmpegPath"];

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
        var algorithm = jobSettings.Algorithm.ToLowerInvariant();

        logger.LogInformation("NonAI: Algorithm {Algorithm}, Scale {Scale}x", algorithm, jobSettings.Scale);

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
        catch (JsonException)
        {
            return new T();
        }
    }
}

// Job settings DTOs (simplified for Worker - could be moved to Shared if needed)
public record RifeJobSettings
{
    public int Multiplier { get; init; } = 2;
    public double? TargetFps { get; init; }
    public string QualityPreset { get; init; } = "Medium";
}

public record RealCuganJobSettings
{
    public int NoiseLevel { get; init; } = -1;
    public int Scale { get; init; } = 2;
    public bool UseFp16 { get; init; } = true;
}

public record RealEsrganJobSettings
{
    public string Model { get; init; } = "realesrgan-x4plus-anime";
    public int Scale { get; init; } = 4;
    public int TileSize { get; init; } = 0;
    public bool UseFp16 { get; init; } = true;
    public bool UseTensorRT { get; init; } = false;
}

public record NonAiJobSettings
{
    public string Algorithm { get; init; } = "lanczos";
    public int Scale { get; init; } = 2;
}
