using SysProcess = System.Diagnostics.Process;
using System.Diagnostics;
using System.Text.RegularExpressions;
using CheapUpscaler.Core.Models;
using CheapUpscaler.Core.Services.VapourSynth;
using Microsoft.Extensions.Logging;

namespace CheapUpscaler.Core.Services.RealCUGAN;

/// <summary>
/// Service wrapper for Real-CUGAN AI upscaling (anime/cartoon optimized)
/// Uses VapourSynth + vs-mlrt plugin with TensorRT/CUDA acceleration
/// Real-CUGAN is 10-13x faster than Real-ESRGAN (~10-20 fps vs ~1 fps on RTX 3080)
/// Architecture matches RealEsrganService for consistency
/// </summary>
public class RealCuganService
{
    private readonly IVapourSynthEnvironment _environment;
    private readonly ILogger<RealCuganService>? _logger;

    public RealCuganService(IVapourSynthEnvironment environment, ILogger<RealCuganService>? logger = null)
    {
        _environment = environment;
        _logger = logger;
        _logger?.LogDebug("RealCuganService initialized with Python: {PythonPath}", _environment.PythonPath);
    }

    /// <summary>
    /// Validate that vs-mlrt is installed and available
    /// </summary>
    public async Task<bool> ValidateInstallationAsync()
    {
        try
        {
            // Check if Python can import vsmlrt
            var (exitCode, output, errorText) = await _environment.RunPythonCommandAsync(
                "-c \"from vsmlrt import CUGAN, Backend; print('OK')\"",
                timeoutMs: 5000
            );

            if (exitCode != 0 || !output.Contains("OK"))
            {
                _logger?.LogWarning("vs-mlrt validation failed: {Error}", errorText);
                return false;
            }

            _logger?.LogDebug("vs-mlrt installation validated successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning("Error validating vs-mlrt installation: {Message}", ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Upscale video using Real-CUGAN via VapourSynth + vs-mlrt pipeline
    /// </summary>
    public async Task<bool> UpscaleVideoAsync(
        string inputVideoPath,
        string outputVideoPath,
        RealCuganOptions options,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default,
        string? ffmpegPath = null)
    {
        if (!File.Exists(inputVideoPath))
            throw new FileNotFoundException($"Input video not found: {inputVideoPath}");

        // Validate noise/scale compatibility
        if (!options.IsNoiseScaleCompatible())
            throw new InvalidOperationException($"Noise level {options.Noise} is not compatible with scale {options.Scale}x. Noise levels 1 and 2 only work with 2x scale.");

        // Ensure output directory exists
        var outputDir = Path.GetDirectoryName(outputVideoPath);
        if (!string.IsNullOrEmpty(outputDir))
            Directory.CreateDirectory(outputDir);

        _logger?.LogDebug("Starting Real-CUGAN upscaling: {Input} -> {Output}", inputVideoPath, outputVideoPath);
        _logger?.LogDebug("Scale: {Scale}x, Noise: {Noise}, Backend: {Backend}, FP16: {UseFp16}", options.Scale, options.Noise, RealCuganOptions.GetBackendDisplayName(options.Backend), options.UseFp16);

        // Check for vspipe (VapourSynth's command-line tool)
        var vspipePath = _environment.VsPipePath;
        if (string.IsNullOrEmpty(vspipePath))
        {
            throw new FileNotFoundException("vspipe.exe not found. Please install VapourSynth.");
        }

        // Create VapourSynth script for Real-CUGAN
        var tempScriptPath = Path.Combine(Path.GetTempPath(), $"realcugan_{Guid.NewGuid().ToString()[..8]}.vpy");

        try
        {
            // Generate VapourSynth script
            var scriptContent = GenerateRealCuganScript(inputVideoPath, options);
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
                    // Use async event-based output reading to prevent deadlock
                    var testOutputBuilder = new System.Text.StringBuilder();
                    var testErrorBuilder = new System.Text.StringBuilder();

                    test.OutputDataReceived += (sender, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                        {
                            testOutputBuilder.AppendLine(e.Data);
                            _logger?.LogDebug("[VapourSynth] {Data}", e.Data);
                        }
                    };

                    test.ErrorDataReceived += (sender, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                        {
                            testErrorBuilder.AppendLine(e.Data);
                            _logger?.LogDebug("[VapourSynth stderr] {Data}", e.Data);
                        }
                    };

                    test.BeginOutputReadLine();
                    test.BeginErrorReadLine();

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

                    var testOutput = testOutputBuilder.ToString();
                    var testError = testErrorBuilder.ToString();

                    // Check if we got valid video information (ignore warnings)
                    bool scriptValid = testOutput.Contains("Width:") &&
                                      testOutput.Contains("Height:") &&
                                      testOutput.Contains("Frames:");

                    if (!scriptValid)
                    {
                        _logger?.LogWarning("VapourSynth script validation failed - no valid video info in output");

                        // Check for Python errors
                        bool hasPythonError = testError.Contains("Error:") ||
                                             testError.Contains("Exception") ||
                                             testError.Contains("Traceback") ||
                                             testError.Contains("ModuleNotFoundError") ||
                                             testError.Contains("ImportError");

                        if (hasPythonError)
                        {
                            throw new InvalidOperationException($"VapourSynth script failed with Python error:\n{testError}");
                        }

                        // Check if TensorRT failed - automatically fall back to CUDA backend
                        if (options.Backend == 0 &&
                            (testError.Contains("TensorRT failed to load") ||
                             testError.Contains("nvinfer") ||
                             testError.Contains("errno 126")))
                        {
                            _logger?.LogInformation("TensorRT not available - falling back to CUDA backend (ORT_CUDA)");

                            // Retry with CUDA backend
                            var fallbackOptions = new RealCuganOptions
                            {
                                Noise = options.Noise,
                                Scale = options.Scale,
                                Backend = 1, // ORT_CUDA
                                UseFp16 = options.UseFp16,
                                GpuId = options.GpuId,
                                NumStreams = options.NumStreams
                            };

                            // Regenerate script with CUDA backend
                            var fallbackScript = GenerateRealCuganScript(inputVideoPath, fallbackOptions);
                            await File.WriteAllTextAsync(tempScriptPath, fallbackScript, cancellationToken);

                            // Update options to use CUDA for the actual processing
                            options.Backend = 1;
                        }
                        else
                        {
                            throw new InvalidOperationException($"Failed to load VapourSynth script: {testError}");
                        }
                    }
                    else
                    {
                        _logger?.LogDebug("VapourSynth script validated successfully");
                    }
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
            _logger?.LogDebug("Starting Real-CUGAN processing pipeline...");

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

            // Pipe vspipe stdout to ffmpeg stdin
            var pipeTask = Task.Run(async () =>
            {
                await vspipe.StandardOutput.BaseStream.CopyToAsync(ffmpeg.StandardInput.BaseStream, cancellationToken);
                ffmpeg.StandardInput.Close();
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

            var success = vspipe.ExitCode == 0 && ffmpeg.ExitCode == 0;

            if (!success)
            {
                _logger?.LogError("Processing failed - vspipe exit: {VspipeExitCode}, ffmpeg exit: {FfmpegExitCode}", vspipe.ExitCode, ffmpeg.ExitCode);
            }
            else
            {
                _logger?.LogDebug("Real-CUGAN upscaling completed successfully: {OutputPath}", outputVideoPath);
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Real-CUGAN upscaling failed: {Message}", ex.Message);
            throw new InvalidOperationException($"Real-CUGAN processing failed: {ex.Message}", ex);
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
    /// Generate VapourSynth script for Real-CUGAN upscaling
    /// Uses vs-mlrt Python wrapper API
    /// </summary>
    private string GenerateRealCuganScript(string inputVideoPath, RealCuganOptions options)
    {
        // Backend configuration
        var backendCode = options.Backend switch
        {
            0 => $"Backend.TRT(fp16={Capitalize(options.UseFp16.ToString())}, device_id={options.GpuId}, num_streams={options.NumStreams})",
            1 => $"Backend.ORT_CUDA(device_id={options.GpuId}, cudnn_benchmark=True, num_streams={options.NumStreams})",
            2 => "Backend.OV_CPU()",
            _ => $"Backend.TRT(fp16={Capitalize(options.UseFp16.ToString())}, device_id={options.GpuId}, num_streams={options.NumStreams})"
        };

        return $@"
import vapoursynth as vs
import sys
import os

core = vs.core

# Add VapourSynth scripts folder to Python path for vsmlrt import
scripts_path = os.path.join(os.environ['APPDATA'], 'VapourSynth', 'scripts')
if scripts_path not in sys.path:
    sys.path.insert(0, scripts_path)

# Try to import vs-mlrt (vsmlrt Python wrapper)
try:
    from vsmlrt import CUGAN, Backend
except ImportError as e:
    raise Exception('vsmlrt not installed. Run: pip install vsmlrt')

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

# Apply Real-CUGAN upscaling via vs-mlrt
try:
    # CUGAN requires RGBS (RGB float32) or RGBH (RGB float16) format
    clip = core.resize.Bicubic(clip, format=vs.RGBS, matrix_in_s='709')

    # Configure backend for processing
    backend = {backendCode}

    # Apply Real-CUGAN upscaling
    clip = CUGAN(
        clip,
        noise={options.Noise},
        scale={options.Scale},
        backend=backend
    )

    # Convert back to YUV420P for Y4M output
    clip = core.resize.Bicubic(clip, format=vs.YUV420P16, matrix_s='709')

except Exception as e:
    import traceback
    error_msg = f'Real-CUGAN upscaling failed: {{str(e)}}'
    print(error_msg, file=sys.stderr)
    traceback.print_exc()
    raise

# Output the processed clip
clip.set_output()
";
    }

    private static string Capitalize(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;
        return char.ToUpper(input[0]) + input[1..].ToLower();
    }

    /// <summary>
    /// Check if Real-CUGAN is available and properly configured
    /// </summary>
    public async Task<bool> IsRealCuganAvailableAsync()
    {
        try
        {
            // Check if Python is available
            if (!await _environment.IsPythonAvailableAsync())
            {
                _logger?.LogWarning("Python not found or not working");
                return false;
            }

            // Check if vs-mlrt is installed
            return await ValidateInstallationAsync();
        }
        catch (Exception ex)
        {
            _logger?.LogWarning("Real-CUGAN availability check failed: {Message}", ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Get list of available noise levels with descriptions
    /// </summary>
    public static (int Value, string Description)[] GetAvailableNoiseLevels()
    {
        return RealCuganOptions.GetAvailableNoiseLevels()
            .Select(n => (n, RealCuganOptions.GetNoiseDisplayName(n)))
            .ToArray();
    }

    /// <summary>
    /// Get list of available scale factors with descriptions
    /// </summary>
    public static (int Value, string Description)[] GetAvailableScales()
    {
        return RealCuganOptions.GetAvailableScaleFactors()
            .Select(s => (s, RealCuganOptions.GetScaleDisplayName(s)))
            .ToArray();
    }
}
