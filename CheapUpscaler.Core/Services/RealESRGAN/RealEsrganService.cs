using SysProcess = System.Diagnostics.Process;
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
        var tempScriptPath = Path.Combine(Path.GetTempPath(), $"realesrgan_{Guid.NewGuid().ToString()[..8]}.vpy");

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
                    var testOutput = await test.StandardOutput.ReadToEndAsync(cancellationToken);
                    var testError = await test.StandardError.ReadToEndAsync(cancellationToken);

                    // Wait up to 10 minutes for model download on first run
                    using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(10));
                    try
                    {
                        await test.WaitForExitAsync(timeoutCts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        _logger?.LogWarning("VapourSynth script test timed out after 10 minutes");
                        try { test.Kill(); } catch { }
                        throw new TimeoutException("VapourSynth script test timed out. Model download may have failed.");
                    }

                    if (test.ExitCode != 0)
                    {
                        _logger?.LogError("VapourSynth script test failed: {Error}", testError);
                        throw new InvalidOperationException($"Failed to load VapourSynth script: {testError}");
                    }

                    _logger?.LogDebug("VapourSynth script validated: {Output}", testOutput);
                }
            }

            // Determine FFmpeg path to use
            var ffmpegExe = ffmpegPath ?? "ffmpeg";

            // Try to find SVP's FFmpeg if not provided
            if (string.IsNullOrEmpty(ffmpegPath) || ffmpegPath == "ffmpeg")
            {
                var svpFFmpeg = @"C:\Program Files (x86)\SVP 4\utils\ffmpeg.exe";
                if (File.Exists(svpFFmpeg))
                {
                    ffmpegExe = svpFFmpeg;
                }
            }

            // Run vspipe -> FFmpeg pipeline
            _logger?.LogDebug("Starting Real-ESRGAN processing pipeline...");

            var vspipeProcess = new ProcessStartInfo
            {
                FileName = vspipePath,
                Arguments = $"\"{tempScriptPath}\" - -c y4m",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            // Use high-quality encoding settings for upscaled output
            var ffmpegProcess = new ProcessStartInfo
            {
                FileName = ffmpegExe,
                Arguments = $"-i - -c:v libx264 -preset slow -crf 18 -pix_fmt yuv420p -y \"{outputVideoPath}\"",
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            _logger?.LogDebug("Pipeline: {VspipeCmd} {VspipeArgs} | {FfmpegCmd} {FfmpegArgs}", vspipeProcess.FileName, vspipeProcess.Arguments, ffmpegProcess.FileName, ffmpegProcess.Arguments);

            // Start both processes and pipe vspipe output to ffmpeg input
            using var vspipe = SysProcess.Start(vspipeProcess);
            using var ffmpeg = SysProcess.Start(ffmpegProcess);

            if (vspipe == null || ffmpeg == null)
            {
                throw new InvalidOperationException("Failed to start vspipe or ffmpeg process");
            }

            // Register cancellation handlers for graceful shutdown
            var vspipeCancellation = cancellationToken.Register(async () =>
            {
                _logger?.LogDebug("Real-ESRGAN cancelled - shutting down vspipe...");
                await ProcessManager.GracefulShutdownAsync(vspipe, gracefulTimeoutMs: 3000, processName: "vspipe (Real-ESRGAN)");
            });

            var ffmpegCancellation = cancellationToken.Register(async () =>
            {
                _logger?.LogDebug("Real-ESRGAN cancelled - shutting down ffmpeg...");
                await ProcessManager.GracefulShutdownAsync(ffmpeg, gracefulTimeoutMs: 2000, processName: "ffmpeg (Real-ESRGAN)");
            });

            try
            {
                // Pipe vspipe stdout to ffmpeg stdin
                var pipeTask = Task.Run(async () =>
                {
                    try
                    {
                        await vspipe.StandardOutput.BaseStream.CopyToAsync(ffmpeg.StandardInput.BaseStream, cancellationToken);
                        ffmpeg.StandardInput.Close();
                    }
                    catch (OperationCanceledException)
                    {
                        _logger?.LogDebug("[Real-ESRGAN] Pipe operation cancelled");
                    }
                }, cancellationToken);

                // Monitor progress from vspipe stderr
                var progressTask = Task.Run(async () =>
                {
                    string? line;
                    var framePattern = new Regex(@"Frame:\s*(\d+)/(\d+)");

                    while ((line = await vspipe.StandardError.ReadLineAsync(cancellationToken)) != null)
                    {
                        _logger?.LogDebug("[vspipe] {Line}", line);

                        var match = framePattern.Match(line);
                        if (match.Success &&
                            int.TryParse(match.Groups[1].Value, out var current) &&
                            int.TryParse(match.Groups[2].Value, out var total) &&
                            total > 0)
                        {
                            progress?.Report((double)current / total * 100);
                        }
                    }
                }, cancellationToken);

                // Monitor FFmpeg output
                var ffmpegMonitorTask = Task.Run(async () =>
                {
                    string? line;
                    while ((line = await ffmpeg.StandardError.ReadLineAsync(cancellationToken)) != null)
                    {
                        _logger?.LogDebug("[ffmpeg] {Line}", line);
                    }
                }, cancellationToken);

                // Wait for all tasks to complete
                await Task.WhenAll(
                    vspipe.WaitForExitAsync(cancellationToken),
                    ffmpeg.WaitForExitAsync(cancellationToken),
                    pipeTask,
                    progressTask,
                    ffmpegMonitorTask
                );
            }
            catch (OperationCanceledException)
            {
                _logger?.LogDebug("Real-ESRGAN processing cancelled");
                throw;
            }
            finally
            {
                vspipeCancellation.Dispose();
                ffmpegCancellation.Dispose();
            }

            var success = vspipe.ExitCode == 0 && ffmpeg.ExitCode == 0;

            if (!success)
            {
                _logger?.LogError("Processing failed - vspipe exit: {VspipeExitCode}, ffmpeg exit: {FfmpegExitCode}", vspipe.ExitCode, ffmpeg.ExitCode);
            }
            else
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
        finally
        {
            // Clean up temp script
            if (File.Exists(tempScriptPath))
            {
                try { File.Delete(tempScriptPath); } catch { }
            }
        }
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
        var tileParam = options.TileMode
            ? $"[{options.TileSize}, {options.TileSize}]"
            : "None";

        // FP16 mode is now done via clip format (RGBH = FP16, RGBS = FP32)
        var clipFormat = options.UseFp16 ? "vs.RGBH" : "vs.RGBS";

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
try:
    clip = core.bs.VideoSource(source=r'{inputVideoPath}')
except:
    try:
        clip = core.ffms2.Source(r'{inputVideoPath}')
    except:
        try:
            clip = core.lsmas.LWLibavSource(r'{inputVideoPath}')
        except:
            try:
                clip = core.avisource.AVISource(r'{inputVideoPath}')
            except Exception as e:
                raise Exception(
                    'No VapourSynth source plugin found. Please install one of: '
                    'BestSource (recommended), ffms2, L-SMASH Source, or AviSource.'
                )

# Get video properties
width = clip.width
height = clip.height
fps = clip.fps

# Apply Real-ESRGAN upscaling
try:
    # Convert to RGB format (required by vsrealesrgan)
    clip = core.resize.Bicubic(clip, format={clipFormat}, matrix_in_s='709')

    # Apply Real-ESRGAN
    clip = realesrgan(
        clip,
        device_index={options.GpuId},
        model=RealESRGANModel.{modelEnumName},
        tile={tileParam},
        tile_pad={options.TilePad},
        trt=False,
        auto_download=True
    )

    # Convert back to YUV420P8 for output
    clip = core.resize.Bicubic(clip, format=vs.YUV420P8, matrix_s='709')
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
