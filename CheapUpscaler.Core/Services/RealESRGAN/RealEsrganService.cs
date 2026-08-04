using SysProcess = System.Diagnostics.Process;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.RegularExpressions;
using CheapUpscaler.Core.Models;
using CheapUpscaler.Core.Services.VapourSynth;
using CheapHelpers.MediaProcessing.Services.Utilities;
using Microsoft.Extensions.Logging;

namespace CheapUpscaler.Core.Services.RealESRGAN;

/// <summary>
/// Service wrapper for Real-ESRGAN AI upscaling
/// Uses VapourSynth + vsrealesrgan plugin with TensorRT/CUDA acceleration
/// Matches the architecture of RifeInterpolationService for consistency
/// </summary>
public class RealEsrganService
{
    private const int NvidiaSmiTimeoutMs = 2000;

    /// <summary>
    /// Total VRAM (MiB) per GPU id, queried once. 0 = query failed / no NVIDIA GPU.
    /// Static because the installed hardware cannot change while the process runs.
    /// </summary>
    private static readonly ConcurrentDictionary<int, int> VramTotalMiBCache = new();

    private readonly IVapourSynthEnvironment _environment;
    private readonly ILogger<RealEsrganService>? _logger;

    public RealEsrganService(IVapourSynthEnvironment environment, ILogger<RealEsrganService>? logger = null)
    {
        _environment = environment;
        _logger = logger;
        _logger?.LogDebug("RealEsrganService initialized with Python: {PythonPath}", _environment.PythonPath);
    }

    /// <summary>
    /// Validate that vsrealesrgan is installed and available
    /// </summary>
    public async Task<bool> ValidateInstallationAsync()
    {
        try
        {
            // Check if Python can import vsrealesrgan
            var (exitCode, output, errorText) = await _environment.RunPythonCommandAsync(
                "-c \"import vsrealesrgan; print('OK')\"",
                timeoutMs: 5000
            );

            if (exitCode != 0 || !output.Contains("OK"))
            {
                _logger?.LogWarning("vsrealesrgan validation failed: {Error}", errorText);
                return false;
            }

            _logger?.LogDebug("vsrealesrgan installation validated successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning("Error validating vsrealesrgan installation: {Message}", ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Upscale video using Real-ESRGAN via VapourSynth pipeline
    /// </summary>
    public async Task<bool> UpscaleVideoAsync(
        string inputVideoPath,
        string outputVideoPath,
        RealEsrganOptions options,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default,
        string? ffmpegPath = null)
    {
        if (!File.Exists(inputVideoPath))
            throw new FileNotFoundException($"Input video not found: {inputVideoPath}");

        // Ensure output directory exists
        var outputDir = Path.GetDirectoryName(outputVideoPath);
        if (!string.IsNullOrEmpty(outputDir))
            Directory.CreateDirectory(outputDir);

        _logger?.LogDebug("Starting Real-ESRGAN upscaling: {Input} -> {Output}", inputVideoPath, outputVideoPath);
        _logger?.LogDebug("Model: {Model}, Scale: {Scale}x, Tile: {TileSize}px", options.ModelName, options.ScaleFactor, options.TileSize);

        // Check for vspipe (VapourSynth's command-line tool)
        var vspipePath = _environment.VsPipePath;
        if (string.IsNullOrEmpty(vspipePath))
        {
            throw new FileNotFoundException("vspipe.exe not found. Please install VapourSynth.");
        }

        // Create VapourSynth script for Real-ESRGAN
        using var tempManager = new TemporaryFileManager();
        var tempScriptPath = tempManager.GetTempFilePath("realesrgan", ".vpy");

        try
        {
            // Generate VapourSynth script
            var scriptContent = GenerateRealEsrganScript(inputVideoPath, options);
            await File.WriteAllTextAsync(tempScriptPath, scriptContent, cancellationToken);

            _logger?.LogDebug("Created VapourSynth script: {ScriptPath}", tempScriptPath);

            // Test if the script loads properly (important for first-time model downloads)
            _logger?.LogDebug("Testing VapourSynth script (may download model on first run)...");

            var testProcess = new ProcessStartInfo
            {
                FileName = vspipePath,
                Arguments = $"--info \"{tempScriptPath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (var test = SysProcess.Start(testProcess))
            {
                if (test != null)
                {
                    // Read both streams in parallel to avoid deadlock
                    // Sequential awaiting causes deadlock when the process fills one pipe buffer
                    var stdoutTask = test.StandardOutput.ReadToEndAsync(cancellationToken);
                    var stderrTask = test.StandardError.ReadToEndAsync(cancellationToken);

                    // Wait up to 10 minutes for model download on first run
                    using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    timeoutCts.CancelAfter(TimeSpan.FromMinutes(10));
                    try
                    {
                        await test.WaitForExitAsync(timeoutCts.Token);
                    }
                    catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                    {
                        _logger?.LogWarning("VapourSynth script test timed out after 10 minutes");
                        try { test.Kill(); } catch { }
                        throw new TimeoutException("VapourSynth script test timed out. Model download may have failed.");
                    }

                    var testOutput = await stdoutTask;
                    var testError = await stderrTask;

                    if (test.ExitCode != 0)
                    {
                        _logger?.LogError("VapourSynth script test failed: {Error}", testError);
                        throw new InvalidOperationException($"Failed to load VapourSynth script: {testError}");
                    }

                    _logger?.LogDebug("VapourSynth script validated: {Output}", testOutput);
                }
            }

            // Run through the shared vspipe -> FFmpeg pipeline
            // (handles progress, cancellation, orphan-kill and audio/subtitle muxing)
            _logger?.LogDebug("Starting Real-ESRGAN processing pipeline...");

            var ffmpegExe = VspipePipeline.ResolveFfmpegPath(ffmpegPath);
            var encodeArgs = VspipePipeline.BuildEncodeArguments(inputVideoPath, outputVideoPath);

            var (success, _, _) = await VspipePipeline.RunAsync(
                vspipePath, tempScriptPath, ffmpegExe, encodeArgs, progress, _logger, cancellationToken);

            if (success)
            {
                _logger?.LogDebug("Real-ESRGAN upscaling completed successfully: {OutputPath}", outputVideoPath);
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Real-ESRGAN upscaling failed: {Message}", ex.Message);
            throw new InvalidOperationException($"Real-ESRGAN processing failed: {ex.Message}", ex);
        }
        // Temp script cleanup handled by TemporaryFileManager.Dispose()
    }

    /// <summary>
    /// Generate VapourSynth script for Real-ESRGAN upscaling
    /// </summary>
    private string GenerateRealEsrganScript(string inputVideoPath, RealEsrganOptions options)
    {
        // Convert model name to vsrealesrgan RealESRGANModel enum name
        var modelEnumName = options.ModelName switch
        {
            "RealESRGAN_x4plus" => "RealESRGAN_x4plus",
            "RealESRGAN_x4plus_anime_6B" => "RealESRGAN_x4plus_anime_6B",
            "RealESRGAN_x2plus" => "RealESRGAN_x2plus",
            "realesr-general-x4v3" => "realesr_general_x4v3",
            "RealESRGAN_AnimeVideo-v3" => "RealESRGAN_AnimeVideo_v3",
            _ => "RealESRGAN_x4plus" // Default to x4plus
        };

        // Tile size - new API expects [width, height]
        var tileParam = ResolveTileParam(options);

        // FP16 mode is now done via clip format (RGBH = FP16, RGBS = FP32)
        var clipFormat = options.UseFp16 ? "vs.RGBH" : "vs.RGBS";

        // TensorRT backend: True = use TensorRT (faster, requires installation), False = use Torch (PyTorch CUDA)
        var useTrt = options.UseTensorRT ? "True" : "False";

        return $@"
import vapoursynth as vs
import sys
import os

core = vs.core

# Try to import vsrealesrgan
try:
    from vsrealesrgan import realesrgan, RealESRGANModel
except ImportError as e:
    raise Exception('vsrealesrgan not installed. Run: pip install vsrealesrgan')

# Load video - try multiple source filters
# Use temp directory for index files to avoid permission issues in Docker
import tempfile
import hashlib
_source = {VspipePipeline.PyQuote(inputVideoPath)}
video_hash = hashlib.md5(_source.encode()).hexdigest()[:8]
index_path = os.path.join(tempfile.gettempdir(), f'ffms2_{{video_hash}}.ffindex')

try:
    clip = core.bs.VideoSource(source=_source)
except:
    try:
        clip = core.ffms2.Source(_source, cachefile=index_path)
    except:
        try:
            clip = core.lsmas.LWLibavSource(_source)
        except:
            try:
                clip = core.avisource.AVISource(_source)
            except Exception as e:
                raise Exception(
                    'No VapourSynth source plugin found. Please install one of: '
                    'BestSource (recommended), ffms2, L-SMASH Source, or AviSource.'
                )

# Get video properties
width = clip.width
height = clip.height
fps = clip.fps

# Detect source color matrix before entering the processing block
{VspipePipeline.MatrixDetectSnippet}
# Apply Real-ESRGAN upscaling
try:
    # Convert to RGB format (required by vsrealesrgan)
    clip = core.resize.Bicubic(clip, format={clipFormat}, matrix_in=_matrix)

    # Apply Real-ESRGAN
    # TensorRT: None = auto-detect, True = force TRT, False = force Torch
    trt_setting = {useTrt}
    clip = realesrgan(
        clip,
        device_index={options.GpuId},
        model=RealESRGANModel.{modelEnumName},
        tile={tileParam},
        tile_pad={options.TilePad},
        trt=trt_setting,
        auto_download=True
    )

    # Convert back to YUV420P8 for output
    clip = core.resize.Bicubic(clip, format=vs.YUV420P8, matrix=_matrix)
except Exception as e:
    import traceback
    error_msg = f'Real-ESRGAN upscaling failed: {{str(e)}}'
    print(error_msg, file=sys.stderr)
    traceback.print_exc()
    raise

# Output the processed clip
clip.set_output()
";
    }

    /// <summary>
    /// Resolve the vsrealesrgan tile= parameter
    /// TileMode off = None, TileSize > 0 = manual override, TileSize &lt;= 0 = auto-select from GPU VRAM
    /// </summary>
    private string ResolveTileParam(RealEsrganOptions options)
    {
        if (!options.TileMode)
            return "None";

        if (options.TileSize > 0)
            return $"[{options.TileSize}, {options.TileSize}]";

        var vramMiB = VramTotalMiBCache.GetOrAdd(options.GpuId, QueryTotalVramMiB);

        // ponytail: static heuristic on TOTAL VRAM. Free VRAM would be more accurate but fluctuates,
        // so it would have to be re-queried per job and could still be stale by the time the model loads.
        // Total is stable and good enough for a one-shot pick.
        // Upgrade path: adaptive retry-on-OOM, halving the tile size on each failed attempt.
        var autoTileSize = vramMiB switch
        {
            >= 12000 => 0,   // no tiling, process full frames
            >= 8000 => 512,
            >= 6000 => 384,
            >= 4000 => 256,
            >= 2000 => 128,
            _ => 64          // tiny VRAM, or query failed (vramMiB == 0)
        };

        _logger?.LogDebug("Auto tile size for GPU {GpuId}: {TileSize} (total VRAM: {VramMiB} MiB{Source})",
            options.GpuId,
            autoTileSize == 0 ? "None (full frame)" : $"{autoTileSize}px",
            vramMiB,
            vramMiB == 0 ? ", unknown - nvidia-smi unavailable" : "");

        return autoTileSize == 0 ? "None" : $"[{autoTileSize}, {autoTileSize}]";
    }

    /// <summary>
    /// Query total VRAM in MiB for a GPU via nvidia-smi. Returns 0 on any failure.
    /// </summary>
    private int QueryTotalVramMiB(int gpuId)
    {
        try
        {
            using var query = SysProcess.Start(new ProcessStartInfo
            {
                FileName = "nvidia-smi",
                Arguments = $"--query-gpu=memory.total --format=csv,noheader,nounits -i {gpuId}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });

            if (query == null)
                return 0;

            // Output is a single short number, so waiting before reading cannot fill the pipe buffer
            if (!query.WaitForExit(NvidiaSmiTimeoutMs))
            {
                try { query.Kill(); } catch { }
                _logger?.LogDebug("nvidia-smi VRAM query timed out for GPU {GpuId}", gpuId);
                return 0;
            }

            var output = query.StandardOutput.ReadToEnd().Trim();
            if (query.ExitCode == 0 && int.TryParse(output, out var totalMiB))
                return totalMiB;

            _logger?.LogDebug("nvidia-smi VRAM query returned exit {ExitCode} for GPU {GpuId}: {Output}",
                query.ExitCode, gpuId, output);
            return 0;
        }
        catch (Exception ex)
        {
            _logger?.LogDebug("nvidia-smi VRAM query failed for GPU {GpuId}: {Message}", gpuId, ex.Message);
            return 0;
        }
    }

    /// <summary>
    /// Check if Real-ESRGAN is available and properly configured
    /// </summary>
    public async Task<bool> IsRealEsrganAvailableAsync()
    {
        try
        {
            // Check if Python is available
            if (!await _environment.IsPythonAvailableAsync())
            {
                _logger?.LogWarning("Python not found or not working");
                return false;
            }

            // Check if vsrealesrgan is installed
            return await ValidateInstallationAsync();
        }
        catch (Exception ex)
        {
            _logger?.LogWarning("Real-ESRGAN availability check failed: {Message}", ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Get list of available Real-ESRGAN models
    /// </summary>
    public static string[] GetAvailableModels()
    {
        return RealEsrganOptions.GetAvailableModels();
    }
}
