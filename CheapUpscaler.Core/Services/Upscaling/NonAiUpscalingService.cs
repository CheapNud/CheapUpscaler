using SysProcess = System.Diagnostics.Process;
using FFMpegCore;
using System.Diagnostics;
using CheapUpscaler.Core.Models;
using CheapHelpers.MediaProcessing.Services;

namespace CheapUpscaler.Core.Services.Upscaling;

/// <summary>
/// Fast non-AI upscaling methods using FFmpeg
/// ULTRA FAST alternatives to Real-ESRGAN (seconds vs hours)
/// - xBR: Pattern-recognition algorithm, great for pixel art/anime, near real-time
/// - Lanczos: Traditional resampling algorithm, smooth results, real-time
/// - HQx: High-quality scaling for pixel art/sprites, real-time
/// </summary>
public class NonAiUpscalingService
{
    private readonly SvpDetectionService _svpDetection;

    public NonAiUpscalingService(SvpDetectionService svpDetection)
    {
        _svpDetection = svpDetection;
        ConfigureFFmpegPath();
    }

    public NonAiUpscalingService() : this(new SvpDetectionService())
    {
    }

    /// <summary>
    /// Configure FFMpegCore to use best available FFmpeg
    /// </summary>
    private void ConfigureFFmpegPath()
    {
        // Auto-detect FFmpeg path
        var ffmpegPath = _svpDetection.GetPreferredFFmpegPath(useSvpEncoders: true);
        if (ffmpegPath != null && Path.IsPathRooted(ffmpegPath))
        {
            var directory = Path.GetDirectoryName(ffmpegPath);
            if (directory != null)
            {
                var ffprobeInSameDir = Path.Combine(directory, "ffprobe.exe");
                if (File.Exists(ffprobeInSameDir))
                {
                    Debug.WriteLine($"[NonAiUpscalingService] Using auto-detected FFmpeg: {directory}");
                    GlobalFFOptions.Configure(new FFOptions { BinaryFolder = directory });
                }
            }
        }
    }

    /// <summary>
    /// Upscale video using xBR algorithm (pattern-recognition based)
    /// ULTRA FAST: Near real-time processing (hundreds of FPS)
    /// Best for: Pixel art, anime, sharp edges
    /// </summary>
    public async Task<bool> UpscaleWithXbrAsync(
        string inputPath,
        string outputPath,
        int scaleFactor,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        // Validate scale factor
        if (scaleFactor < 2 || scaleFactor > 4)
        {
            Debug.WriteLine($"ERROR: xBR scale factor must be 2, 3, or 4 (got {scaleFactor})");
            return false;
        }

        Debug.WriteLine($"[xBR] Starting {scaleFactor}x upscale: {inputPath} -> {outputPath}");

        try
        {
            var mediaInfo = await FFProbe.AnalyseAsync(inputPath, cancellationToken: cancellationToken);
            Debug.WriteLine($"[xBR] Input: {mediaInfo.PrimaryVideoStream?.Width}x{mediaInfo.PrimaryVideoStream?.Height}");
            Debug.WriteLine($"[xBR] Duration: {mediaInfo.Duration.TotalSeconds:F1} seconds");

            var processor = FFMpegArguments
                .FromFileInput(inputPath)
                .OutputToFile(outputPath, true, options => options
                    .WithCustomArgument($"-filter:v xbr={scaleFactor}")
                    .WithCustomArgument("-c:a copy") // Copy audio without re-encoding
                    .WithCustomArgument("-pix_fmt yuv420p")); // Compatibility

            // Add progress tracking
            if (progress != null)
            {
                processor = processor.NotifyOnProgress(percentage =>
                {
                    progress.Report(percentage);
                }, mediaInfo.Duration);
            }

            await processor.CancellableThrough(cancellationToken).ProcessAsynchronously();

            Debug.WriteLine($"[xBR] Upscale complete: {outputPath}");
            return true;
        }
        catch (OperationCanceledException)
        {
            Debug.WriteLine("[xBR] Upscale cancelled");
            return false;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[xBR] Upscale error: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Upscale video using Lanczos algorithm (traditional resampling)
    /// ULTRA FAST: Real-time processing (hundreds of FPS)
    /// Best for: Smooth gradients, photos, general content
    /// </summary>
    public async Task<bool> UpscaleWithLanczosAsync(
        string inputPath,
        string outputPath,
        int scaleFactor,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        // Validate scale factor
        if (scaleFactor < 2 || scaleFactor > 4)
        {
            Debug.WriteLine($"ERROR: Lanczos scale factor must be 2, 3, or 4 (got {scaleFactor})");
            return false;
        }

        Debug.WriteLine($"[Lanczos] Starting {scaleFactor}x upscale: {inputPath} -> {outputPath}");

        try
        {
            var mediaInfo = await FFProbe.AnalyseAsync(inputPath, cancellationToken: cancellationToken);
            Debug.WriteLine($"[Lanczos] Input: {mediaInfo.PrimaryVideoStream?.Width}x{mediaInfo.PrimaryVideoStream?.Height}");
            Debug.WriteLine($"[Lanczos] Duration: {mediaInfo.Duration.TotalSeconds:F1} seconds");

            var processor = FFMpegArguments
                .FromFileInput(inputPath)
                .OutputToFile(outputPath, true, options => options
                    .WithCustomArgument($"-vf scale=iw*{scaleFactor}:ih*{scaleFactor}:flags=lanczos")
                    .WithCustomArgument("-c:a copy") // Copy audio without re-encoding
                    .WithCustomArgument("-pix_fmt yuv420p")); // Compatibility

            // Add progress tracking
            if (progress != null)
            {
                processor = processor.NotifyOnProgress(percentage =>
                {
                    progress.Report(percentage);
                }, mediaInfo.Duration);
            }

            await processor.CancellableThrough(cancellationToken).ProcessAsynchronously();

            Debug.WriteLine($"[Lanczos] Upscale complete: {outputPath}");
            return true;
        }
        catch (OperationCanceledException)
        {
            Debug.WriteLine("[Lanczos] Upscale cancelled");
            return false;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Lanczos] Upscale error: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Upscale video using HQx algorithm (high-quality magnification)
    /// ULTRA FAST: Near real-time processing (hundreds of FPS)
    /// Best for: Pixel art, sprites, retro games
    /// </summary>
    public async Task<bool> UpscaleWithHqxAsync(
        string inputPath,
        string outputPath,
        int scaleFactor,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        // Validate scale factor
        if (scaleFactor < 2 || scaleFactor > 4)
        {
            Debug.WriteLine($"ERROR: HQx scale factor must be 2, 3, or 4 (got {scaleFactor})");
            return false;
        }

        Debug.WriteLine($"[HQx] Starting {scaleFactor}x upscale: {inputPath} -> {outputPath}");

        try
        {
            var mediaInfo = await FFProbe.AnalyseAsync(inputPath, cancellationToken: cancellationToken);
            Debug.WriteLine($"[HQx] Input: {mediaInfo.PrimaryVideoStream?.Width}x{mediaInfo.PrimaryVideoStream?.Height}");
            Debug.WriteLine($"[HQx] Duration: {mediaInfo.Duration.TotalSeconds:F1} seconds");

            var processor = FFMpegArguments
                .FromFileInput(inputPath)
                .OutputToFile(outputPath, true, options => options
                    .WithCustomArgument($"-filter:v hqx={scaleFactor}")
                    .WithCustomArgument("-c:a copy") // Copy audio without re-encoding
                    .WithCustomArgument("-pix_fmt yuv420p")); // Compatibility

            // Add progress tracking
            if (progress != null)
            {
                processor = processor.NotifyOnProgress(percentage =>
                {
                    progress.Report(percentage);
                }, mediaInfo.Duration);
            }

            await processor.CancellableThrough(cancellationToken).ProcessAsynchronously();

            Debug.WriteLine($"[HQx] Upscale complete: {outputPath}");
            return true;
        }
        catch (OperationCanceledException)
        {
            Debug.WriteLine("[HQx] Upscale cancelled");
            return false;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[HQx] Upscale error: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Generic upscale method that dispatches to the appropriate algorithm
    /// </summary>
    public async Task<bool> UpscaleVideoAsync(
        string inputPath,
        string outputPath,
        string algorithm,
        int scaleFactor,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        Debug.WriteLine($"[NonAI Upscaling] Algorithm: {algorithm}, Scale: {scaleFactor}x");

        return algorithm.ToLower() switch
        {
            "xbr" => await UpscaleWithXbrAsync(inputPath, outputPath, scaleFactor, progress, cancellationToken),
            "lanczos" => await UpscaleWithLanczosAsync(inputPath, outputPath, scaleFactor, progress, cancellationToken),
            "hqx" => await UpscaleWithHqxAsync(inputPath, outputPath, scaleFactor, progress, cancellationToken),
            _ => throw new ArgumentException($"Unknown upscaling algorithm: {algorithm}")
        };
    }

    /// <summary>
    /// Check if FFmpeg supports the specified upscaling filter
    /// </summary>
    public async Task<bool> IsFilterSupportedAsync(string filterName)
    {
        try
        {
            var ffmpegPath = _svpDetection.GetPreferredFFmpegPath(useSvpEncoders: true) ?? "ffmpeg";

            var process = new SysProcess
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = ffmpegPath,
                    Arguments = "-filters",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var outputText = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            var supported = outputText.Contains($" {filterName} ", StringComparison.OrdinalIgnoreCase);
            Debug.WriteLine($"[NonAI Upscaling] Filter '{filterName}' supported: {supported}");

            return supported;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[NonAI Upscaling] Error checking filter support: {ex.Message}");
            return false;
        }
    }
}
