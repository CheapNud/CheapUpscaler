using SysProcess = System.Diagnostics.Process;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using CheapHelpers.MediaProcessing.Services.Utilities;
using Microsoft.Extensions.Logging;

namespace CheapUpscaler.Core.Services.RIFE;

/// <summary>
/// Service wrapper for RIFE (Real-Time Intermediate Flow Estimation)
/// Supports:
/// - SVP's integrated RIFE (VapourSynth-based with TensorRT)
/// - Practical-RIFE standalone (https://github.com/hzwer/Practical-RIFE)
/// </summary>
public class RifeInterpolationService
{
    private readonly string _rifeFolderPath;
    private readonly string _pythonPath;
    private readonly ILogger<RifeInterpolationService>? _logger;
    private bool? _isSvpRife;
    private bool _isValidated;

    /// <summary>
    /// Indicates whether RIFE is configured and available for use
    /// </summary>
    public bool IsConfigured => !string.IsNullOrEmpty(_rifeFolderPath) && Directory.Exists(_rifeFolderPath);

    /// <summary>
    /// Gets the list of available RIFE ONNX models installed in the SVP models folder.
    /// Returns model names in the format expected by RifeOptions (e.g., "rife-v4.6", "rife-v4.22-lite")
    /// </summary>
    public List<string> GetAvailableModels()
    {
        if (!IsConfigured)
            return [];

        var rifeModelDir = Path.Combine(_rifeFolderPath, "models", "rife");
        if (!Directory.Exists(rifeModelDir))
            return [];

        return Directory.GetFiles(rifeModelDir, "*.onnx")
            .Select(f => MapOnnxFilenameToModelName(Path.GetFileNameWithoutExtension(f)))
            .Where(name => name != null)
            .Cast<string>()
            .Distinct()
            .OrderBy(name => name)
            .ToList();
    }

    /// <summary>
    /// Checks if a specific model is available for use
    /// </summary>
    public bool IsModelAvailable(string modelName)
    {
        var available = GetAvailableModels();
        return available.Contains(modelName, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Auto-detect the best available engine based on installed model files.
    /// Priority: TensorRT (ONNX) > NCNN (bin/param) > Vulkan
    /// </summary>
    public RifeEngine AutoSelectEngine()
    {
        if (!IsConfigured)
            return RifeEngine.TensorRT; // Default, will fail at runtime with helpful error

        var rifeModelDir = Path.Combine(_rifeFolderPath, "models", "rife");
        if (!Directory.Exists(rifeModelDir))
            return RifeEngine.TensorRT;

        // Check for ONNX files (TensorRT)
        var hasOnnx = Directory.GetFiles(rifeModelDir, "*.onnx").Length > 0;
        if (hasOnnx)
        {
            _logger?.LogDebug("[RIFE] ONNX models found, selecting TensorRT engine");
            return RifeEngine.TensorRT;
        }

        // Check for NCNN files (.bin and .param pairs)
        var hasBin = Directory.GetFiles(rifeModelDir, "*.bin").Length > 0;
        var hasParam = Directory.GetFiles(rifeModelDir, "*.param").Length > 0;
        if (hasBin && hasParam)
        {
            _logger?.LogDebug("[RIFE] NCNN models found, selecting NCNN engine");
            return RifeEngine.NCNN;
        }

        // Fallback to TensorRT (most common with SVP)
        _logger?.LogDebug("[RIFE] No specific model files found, defaulting to TensorRT");
        return RifeEngine.TensorRT;
    }

    /// <summary>
    /// Get available engines based on installed model files
    /// </summary>
    public List<RifeEngine> GetAvailableEngines()
    {
        var engines = new List<RifeEngine>();

        if (!IsConfigured)
            return engines;

        var rifeModelDir = Path.Combine(_rifeFolderPath, "models", "rife");
        if (!Directory.Exists(rifeModelDir))
            return engines;

        // Check for ONNX files (TensorRT)
        if (Directory.GetFiles(rifeModelDir, "*.onnx").Length > 0)
            engines.Add(RifeEngine.TensorRT);

        // Check for NCNN files
        if (Directory.GetFiles(rifeModelDir, "*.bin").Length > 0 &&
            Directory.GetFiles(rifeModelDir, "*.param").Length > 0)
            engines.Add(RifeEngine.NCNN);

        return engines;
    }

    /// <summary>
    /// Maps ONNX filename (without extension) to our model name format
    /// </summary>
    private static string? MapOnnxFilenameToModelName(string onnxName)
    {
        // Map SVP's ONNX filenames to our model names
        // e.g., "rife_v4.6" -> "rife-v4.6", "rife_v4.22_lite" -> "rife-v4.22-lite"
        return onnxName.ToLower() switch
        {
            "rife_v4.6" => "rife-v4.6",
            "rife_v4.14" => "rife-v4.14",
            "rife_v4.14_lite" => "rife-v4.14-lite",
            "rife_v4.15" => "rife-v4.15",
            "rife_v4.15_lite" => "rife-v4.15-lite",
            "rife_v4.16" => "rife-v4.16",
            "rife_v4.16_lite" => "rife-v4.16-lite",
            "rife_v4.17" => "rife-v4.17",
            "rife_v4.18" => "rife-v4.18",
            "rife_v4.20" => "rife-v4.20",
            "rife_v4.21" => "rife-v4.21",
            "rife_v4.22" => "rife-v4.22",
            "rife_v4.22_lite" => "rife-v4.22-lite",
            "rife_v4.25" => "rife-v4.25",
            "rife_v4.25_lite" => "rife-v4.25-lite",
            "rife_v4.26" => "rife-v4.26",
            "rife_v4.9_uhd" => "rife-UHD",
            "rife_v4.8_anime" => "rife-anime",
            _ => null // Unknown model
        };
    }

    public RifeInterpolationService(string rifeFolderPath = "", string pythonPath = "", ILogger<RifeInterpolationService>? logger = null)
    {
        _rifeFolderPath = rifeFolderPath;
        _logger = logger;

        // Auto-detect Python path if not specified
        if (string.IsNullOrEmpty(pythonPath))
        {
            // On Windows, try "python" first, then "python3"
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                _pythonPath = IsPythonAvailable("python") ? "python" :
                              IsPythonAvailable("python3") ? "python3" : "python";
            }
            else
            {
                _pythonPath = "python3";
            }
        }
        else
        {
            _pythonPath = pythonPath;
        }

        // Don't validate in constructor - defer until first use
        // This allows DI to create the service even if RIFE isn't installed
        if (!string.IsNullOrEmpty(_rifeFolderPath))
        {
            _isSvpRife = DetectRifeType();
        }
    }

    private bool IsSvpRife
    {
        get
        {
            _isSvpRife ??= DetectRifeType();
            return _isSvpRife.Value;
        }
    }

    /// <summary>
    /// Check if Python is available in PATH
    /// </summary>
    private bool IsPythonAvailable(string pythonCommand)
    {
        try
        {
            var process = new SysProcess
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = pythonCommand,
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            process.WaitForExit(2000);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Detect if this is SVP's RIFE (has rife.dll) or GitHub RIFE (has inference_video.py)
    /// </summary>
    private bool DetectRifeType()
    {
        if (string.IsNullOrEmpty(_rifeFolderPath))
            return false;

        // Check for SVP's RIFE files
        if (File.Exists(Path.Combine(_rifeFolderPath, "rife.dll")) ||
            File.Exists(Path.Combine(_rifeFolderPath, "rife_vs.dll")))
        {
            _logger?.LogDebug("Detected SVP's RIFE installation");
            return true;
        }

        // Check for GitHub RIFE
        if (File.Exists(Path.Combine(_rifeFolderPath, "inference_video.py")))
        {
            _logger?.LogDebug("Detected GitHub RIFE repository");
            return false;
        }

        _logger?.LogDebug("Unknown RIFE installation type");
        return false;
    }

    /// <summary>
    /// Validate that RIFE folder exists and contains required files
    /// Called lazily when service methods are invoked
    /// </summary>
    private void EnsureValidated()
    {
        if (_isValidated) return;

        if (string.IsNullOrEmpty(_rifeFolderPath))
        {
            _logger?.LogWarning("RIFE folder path not configured");
            throw new InvalidOperationException("RIFE is not configured. Please install RIFE and configure the path in Settings.");
        }

        if (!Directory.Exists(_rifeFolderPath))
        {
            _logger?.LogWarning("RIFE folder not found at: {RifeFolderPath}", _rifeFolderPath);
            throw new DirectoryNotFoundException($"RIFE folder not found: {_rifeFolderPath}");
        }

        // Validate based on type
        if (IsSvpRife)
        {
            // Check for SVP RIFE files
            var requiredFiles = new[] { "rife.dll", "rife_vs.dll", "vsmirt.py", "vstrt.dll" };
            var foundAny = requiredFiles.Any(f => File.Exists(Path.Combine(_rifeFolderPath, f)));

            if (!foundAny)
            {
                _logger?.LogWarning("SVP RIFE files not found in: {RifeFolderPath}", _rifeFolderPath);
                throw new FileNotFoundException($"SVP RIFE files not found in: {_rifeFolderPath}");
            }
        }
        else
        {
            // Check for GitHub RIFE files
            var scriptPath = Path.Combine(_rifeFolderPath, "inference_video.py");
            if (!File.Exists(scriptPath))
            {
                _logger?.LogWarning("inference_video.py not found in: {RifeFolderPath}", _rifeFolderPath);
                throw new FileNotFoundException($"inference_video.py not found in: {_rifeFolderPath}");
            }
        }

        _isValidated = true;
    }

    /// <summary>
    /// Interpolate video using RIFE (direct video-to-video)
    /// </summary>
    public async Task<bool> InterpolateVideoAsync(
        string inputVideoPath,
        string outputVideoPath,
        RifeOptions options,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default,
        string? ffmpegPath = null)
    {
        EnsureValidated();

        if (!File.Exists(inputVideoPath))
            throw new FileNotFoundException($"Input video not found: {inputVideoPath}");

        // Ensure output directory exists
        var outputDir = Path.GetDirectoryName(outputVideoPath);
        if (!string.IsNullOrEmpty(outputDir))
            Directory.CreateDirectory(outputDir);

        string arguments;
        string pythonScript;

        if (IsSvpRife)
        {
            // SVP's RIFE uses VapourSynth integration
            _logger?.LogDebug("Attempting SVP RIFE interpolation via VapourSynth...");

            // Check for vspipe (VapourSynth's command-line tool)
            var vspipePath = FindVsPipe();
            if (string.IsNullOrEmpty(vspipePath))
            {
                throw new FileNotFoundException("vspipe.exe not found. Please install VapourSynth or ensure it's in PATH.");
            }

            // Create a VapourSynth script for SVP RIFE
            using var tempManager = new TemporaryFileManager();
            var tempScriptPath = tempManager.GetTempFilePath("svp_rife", ".vpy");

            try
            {
                // Generate VapourSynth script for SVP RIFE
                var scriptContent = GenerateSvpRifeScript(inputVideoPath, options);
                await File.WriteAllTextAsync(tempScriptPath, scriptContent, cancellationToken);

                _logger?.LogDebug($"Created VapourSynth script: {tempScriptPath}");

                // First, test if the script loads properly (streams output in real-time for debugging)
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
                        _logger?.LogDebug("Testing VapourSynth script (TensorRT initialization may take 5-15 minutes on first run)...");

                        var outputBuilder = new System.Text.StringBuilder();
                        var errorBuilder = new System.Text.StringBuilder();

                        // Stream output in real-time for debugging
                        var stdoutTask = Task.Run(async () =>
                        {
                            string? line;
                            while ((line = await test.StandardOutput.ReadLineAsync(cancellationToken)) != null)
                            {
                                _logger?.LogDebug($"[vspipe stdout] {line}");
                                outputBuilder.AppendLine(line);
                            }
                        }, cancellationToken);

                        var stderrTask = Task.Run(async () =>
                        {
                            string? line;
                            while ((line = await test.StandardError.ReadLineAsync(cancellationToken)) != null)
                            {
                                _logger?.LogDebug($"[vspipe stderr] {line}");
                                errorBuilder.AppendLine(line);
                            }
                        }, cancellationToken);

                        // Wait up to 20 minutes for TensorRT to compile on first run
                        var timeoutMs = 20 * 60 * 1000; // 20 minutes
                        var completed = test.WaitForExit(timeoutMs);

                        if (!completed)
                        {
                            _logger?.LogWarning("VapourSynth script test timed out after 20 minutes");
                            try { test.Kill(); } catch { }
                            throw new TimeoutException("VapourSynth script test timed out. TensorRT initialization may have failed.");
                        }

                        // Wait for output tasks to complete
                        await Task.WhenAll(stdoutTask, stderrTask);

                        var testOutput = outputBuilder.ToString();
                        var testError = errorBuilder.ToString();

                        if (test.ExitCode != 0)
                        {
                            _logger?.LogError("VapourSynth script test failed with exit code {ExitCode}", test.ExitCode);
                            _logger?.LogError("VapourSynth stderr: {StdErr}", testError);
                            throw new InvalidOperationException($"Failed to load VapourSynth script: {testError}");
                        }

                        _logger?.LogDebug($"VapourSynth script test passed. Output: {testOutput}");
                    }
                }

                // Now run the actual processing with vspipe piped to FFmpeg
                // -p enables progress reporting to stderr (Frame: X/Y format)
                var vspipeProcess = new ProcessStartInfo
                {
                    FileName = vspipePath,
                    Arguments = $"-p \"{tempScriptPath}\" - -c y4m",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

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

                var ffmpegProcess = new ProcessStartInfo
                {
                    FileName = ffmpegExe,
                    Arguments = $"-i - -c:v libx264 -preset fast -crf 18 -y \"{outputVideoPath}\"",
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                _logger?.LogDebug($"Running: {vspipeProcess.FileName} {vspipeProcess.Arguments} | {ffmpegProcess.FileName} {ffmpegProcess.Arguments}");

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
                    _logger?.LogDebug("RIFE (SVP) cancelled - shutting down vspipe...");
                    await ProcessManager.GracefulShutdownAsync(vspipe, gracefulTimeoutMs: 3000, processName: "vspipe (RIFE)");
                });

                var ffmpegCancellation = cancellationToken.Register(async () =>
                {
                    _logger?.LogDebug("RIFE (SVP) cancelled - shutting down ffmpeg...");
                    await ProcessManager.GracefulShutdownAsync(ffmpeg, gracefulTimeoutMs: 2000, processName: "ffmpeg (RIFE)");
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
                            _logger?.LogDebug("[RIFE] Pipe operation cancelled");
                        }
                    }, cancellationToken);

                    // Monitor progress from vspipe stderr
                    var progressTask = Task.Run(async () =>
                    {
                        string? line;
                        var framePattern = new Regex(@"Frame:\s*(\d+)/(\d+)");

                        while ((line = await vspipe.StandardError.ReadLineAsync(cancellationToken)) != null)
                        {
                            _logger?.LogDebug($"[vspipe] {line}");

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
                            _logger?.LogDebug($"[ffmpeg] {line}");
                        }
                    }, cancellationToken);

                    // Wait for both processes
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
                    _logger?.LogDebug("RIFE (SVP) processing cancelled");
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

                return success;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "SVP RIFE VapourSynth processing failed: {Message}", ex.Message);
                throw new InvalidOperationException($"SVP RIFE processing failed: {ex.Message}", ex);
            }
            // Temp script cleanup handled by TemporaryFileManager.Dispose()
        }
        else
        {
            // GitHub Practical-RIFE uses inference_video.py
            pythonScript = Path.Combine(_rifeFolderPath, "inference_video.py");

            arguments = $"\"{pythonScript}\" --video=\"{inputVideoPath}\" --output=\"{outputVideoPath}\" --multi={options.InterpolationMultiplier}";

            // Add optional parameters for Practical-RIFE
            if (!string.IsNullOrEmpty(options.ModelName))
            {
                var modelVersion = options.ModelName.Replace("rife-v", "").Replace("rife-", "");
                arguments += $" --model={modelVersion}";
            }

            if (options.Scale > 0 && options.Scale != 1.0)
            {
                arguments += $" --scale={options.Scale:F1}";
            }

            if (options.UhdMode)
            {
                arguments += " --uhd";
            }

            if (options.GpuId >= 0)
            {
                arguments += $" --gpu={options.GpuId}";
            }
        }

        _logger?.LogDebug($"Starting RIFE interpolation: {_pythonPath} {arguments}");

        try
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = _pythonPath,
                Arguments = arguments,
                WorkingDirectory = _rifeFolderPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new SysProcess { StartInfo = processInfo };

            // Track progress from output
            var progressPattern = new Regex(@"(\d+)/(\d+)");
            var percentPattern = new Regex(@"(\d+)%");

            process.OutputDataReceived += (sender, e) =>
            {
                if (string.IsNullOrEmpty(e.Data))
                    return;

                _logger?.LogDebug($"[RIFE] {e.Data}");

                // Try to extract progress
                var percentMatch = percentPattern.Match(e.Data);
                if (percentMatch.Success && double.TryParse(percentMatch.Groups[1].Value, out var percent))
                {
                    progress?.Report(percent);
                }
                else
                {
                    var progressMatch = progressPattern.Match(e.Data);
                    if (progressMatch.Success &&
                        int.TryParse(progressMatch.Groups[1].Value, out var current) &&
                        int.TryParse(progressMatch.Groups[2].Value, out var total) &&
                        total > 0)
                    {
                        progress?.Report((double)current / total * 100);
                    }
                }
            };

            process.ErrorDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    // stderr often contains progress info, not just errors
                    _logger?.LogDebug("[RIFE stderr] {Data}", e.Data);
                }
            };

            // Register cancellation handler for graceful shutdown
            var rifeCancellation = cancellationToken.Register(async () =>
            {
                _logger?.LogDebug("RIFE (Python) cancelled - initiating graceful shutdown...");
                await ProcessManager.GracefulShutdownAsync(process, gracefulTimeoutMs: 3000, processName: "RIFE (Python)");
            });

            try
            {
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                // Wait for completion with cancellation support
                await process.WaitForExitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                _logger?.LogDebug("RIFE (Python) processing cancelled");
                return false;
            }
            finally
            {
                rifeCancellation.Dispose();
            }

            var success = process.ExitCode == 0;

            if (success && !IsSvpRife)
            {
                if (!File.Exists(outputVideoPath))
                {
                    var expectedOutput = Path.Combine(
                        Path.GetDirectoryName(inputVideoPath) ?? "",
                        Path.GetFileNameWithoutExtension(inputVideoPath) + $"_{options.InterpolationMultiplier}X_" +
                        $"{options.TargetFps}fps.mp4"
                    );

                    if (File.Exists(expectedOutput))
                    {
                        File.Move(expectedOutput, outputVideoPath, overwrite: true);
                        _logger?.LogDebug("Moved RIFE output from {ExpectedOutput} to {OutputVideoPath}", expectedOutput, outputVideoPath);
                    }
                    else
                    {
                        _logger?.LogWarning("RIFE output file not found at expected locations");
                        success = false;
                    }
                }
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "RIFE interpolation failed: {Message}", ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Interpolate frames in a directory (for pipeline usage)
    /// </summary>
    public async Task<bool> InterpolateFramesAsync(
        string inputFramesFolder,
        string outputFramesFolder,
        RifeOptions options,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default,
        string? ffmpegPath = null)
    {
        if (!Directory.Exists(inputFramesFolder))
            throw new DirectoryNotFoundException($"Input frames folder not found: {inputFramesFolder}");

        Directory.CreateDirectory(outputFramesFolder);

        // Get frame files
        var frameFiles = Directory.GetFiles(inputFramesFolder, "*.png")
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (frameFiles.Length == 0)
        {
            _logger?.LogDebug("No PNG frames found in input folder");
            return false;
        }

        _logger?.LogDebug($"Found {frameFiles.Length} frames to interpolate");

        // Use provided FFmpeg path or try to find it
        if (string.IsNullOrEmpty(ffmpegPath))
        {
            ffmpegPath = "ffmpeg";
            var svpFFmpeg = @"C:\Program Files (x86)\SVP 4\utils\ffmpeg.exe";
            if (File.Exists(svpFFmpeg))
            {
                ffmpegPath = svpFFmpeg;
            }
        }

        // Create temporary video from frames
        var tempVideoIn = Path.Combine(Path.GetTempPath(), $"rife_temp_{Guid.NewGuid().ToString()[..8]}.mp4");
        var tempVideoOut = Path.Combine(Path.GetTempPath(), $"rife_out_{Guid.NewGuid().ToString()[..8]}.mp4");

        try
        {
            // Step 1: Convert frames to video using FFmpeg
            var framePath = Path.Combine(inputFramesFolder, "frame_%06d.png");
            var ffmpegArgs = $"-y -framerate 30 -i \"{framePath}\" -c:v libx264 -preset fast -crf 0 -pix_fmt yuv420p \"{tempVideoIn}\"";

            _logger?.LogDebug($"Creating temp video from frames: {ffmpegPath} {ffmpegArgs}");

            var ffmpegProcess = new SysProcess
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = ffmpegPath,
                    Arguments = ffmpegArgs,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            var errorOutput = new System.Text.StringBuilder();
            ffmpegProcess.ErrorDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    errorOutput.AppendLine(e.Data);
                    _logger?.LogDebug($"[FFmpeg] {e.Data}");
                }
            };

            ffmpegProcess.Start();
            ffmpegProcess.BeginErrorReadLine();

            var completed = ffmpegProcess.WaitForExit(300000); // 5 minutes

            if (!completed)
            {
                _logger?.LogWarning("FFmpeg process timed out after 5 minutes");
                try { ffmpegProcess.Kill(); } catch { }
                return false;
            }

            if (ffmpegProcess.ExitCode != 0 || !File.Exists(tempVideoIn))
            {
                _logger?.LogError("FFmpeg failed with exit code: {ExitCode}", ffmpegProcess.ExitCode);
                _logger?.LogError("FFmpeg error output: {ErrorOutput}", errorOutput);
                return false;
            }

            // Step 2: Run RIFE interpolation on the video
            progress?.Report(30);

            var interpolationSuccess = await InterpolateVideoAsync(
                tempVideoIn,
                tempVideoOut,
                options,
                new Progress<double>(p => progress?.Report(30 + p * 0.4)),
                cancellationToken,
                ffmpegPath);

            if (!interpolationSuccess || !File.Exists(tempVideoOut))
            {
                _logger?.LogError("RIFE interpolation failed for frames");
                return false;
            }

            // Step 3: Extract frames from interpolated video
            progress?.Report(70);

            var outputFramePath = Path.Combine(outputFramesFolder, "frame_%06d.png");
            var extractArgs = $"-y -i \"{tempVideoOut}\" \"{outputFramePath}\"";

            _logger?.LogDebug($"Extracting interpolated frames: {ffmpegPath} {extractArgs}");

            var extractErrorOutput = new System.Text.StringBuilder();
            var extractProcess = new SysProcess
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = ffmpegPath,
                    Arguments = extractArgs,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            extractProcess.ErrorDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    extractErrorOutput.AppendLine(e.Data);
                    _logger?.LogDebug($"[FFmpeg Extract] {e.Data}");
                }
            };

            extractProcess.Start();
            extractProcess.BeginErrorReadLine();

            var extractCompleted = extractProcess.WaitForExit(300000); // 5 minutes

            if (!extractCompleted)
            {
                _logger?.LogWarning("FFmpeg extraction timed out after 5 minutes");
                try { extractProcess.Kill(); } catch { }
                return false;
            }

            if (extractProcess.ExitCode != 0)
            {
                _logger?.LogError("FFmpeg extract failed with exit code: {ExitCode}", extractProcess.ExitCode);
                _logger?.LogError("FFmpeg extract error: {ErrorOutput}", extractErrorOutput);
                return false;
            }

            progress?.Report(100);

            // Verify output frames were created
            var outputFrames = Directory.GetFiles(outputFramesFolder, "*.png");
            _logger?.LogDebug($"Extracted {outputFrames.Length} interpolated frames");

            return outputFrames.Length > 0;
        }
        finally
        {
            // Clean up temp files
            if (File.Exists(tempVideoIn))
            {
                try { File.Delete(tempVideoIn); } catch { }
            }
            if (File.Exists(tempVideoOut))
            {
                try { File.Delete(tempVideoOut); } catch { }
            }
        }
    }

    /// <summary>
    /// Get all supported RIFE model names (static list of known models).
    /// Use GetAvailableModels() instance method to get actually installed models.
    /// </summary>
    public static string[] GetSupportedModels()
    {
        return
        [
            "rife-v4.6",
            "rife-v4.14",
            "rife-v4.14-lite",
            "rife-v4.15",
            "rife-v4.15-lite",
            "rife-v4.16",
            "rife-v4.16-lite",
            "rife-v4.17",
            "rife-v4.18",
            "rife-v4.20",
            "rife-v4.21",
            "rife-v4.22",
            "rife-v4.22-lite",
            "rife-v4.25",
            "rife-v4.25-lite",
            "rife-v4.26",
            "rife-anime",
            "rife-UHD"
        ];
    }

    /// <summary>
    /// Find vspipe executable
    /// </summary>
    private string? FindVsPipe()
    {
        var possiblePaths = new[]
        {
            @"C:\Program Files\VapourSynth\core\vspipe.exe",
            @"C:\Program Files (x86)\VapourSynth\core\vspipe.exe",
            @"C:\Python311\Scripts\vspipe.exe",
            @"C:\Python310\Scripts\vspipe.exe",
            @"C:\Python39\Scripts\vspipe.exe",
            @"C:\Python38\Scripts\vspipe.exe",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"VapourSynth\core\vspipe.exe"),
        };

        foreach (var path in possiblePaths)
        {
            if (File.Exists(path))
            {
                _logger?.LogDebug($"Found vspipe at: {path}");
                return path;
            }
        }

        // Try to find in PATH
        try
        {
            var process = new SysProcess
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "vspipe",
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            process.WaitForExit(2000);

            if (process.ExitCode == 0)
            {
                _logger?.LogDebug("Found vspipe in PATH");
                return "vspipe";
            }
        }
        catch
        {
            // vspipe not in PATH
        }

        _logger?.LogDebug("vspipe not found");
        return null;
    }

    /// <summary>
    /// Generate a VapourSynth script for SVP RIFE processing
    /// </summary>
    private string GenerateSvpRifeScript(string inputVideoPath, RifeOptions options)
    {
        var multiplier = options.GetFrameMultiplier();

        // Validate RIFE folder path exists before generating script
        if (string.IsNullOrEmpty(_rifeFolderPath))
            throw new InvalidOperationException("RIFE folder path is not configured. Install SVP 4 Pro or configure RIFE path in Settings.");

        if (!Directory.Exists(_rifeFolderPath))
            throw new DirectoryNotFoundException($"RIFE folder not found: {_rifeFolderPath}");

        var pluginPath = Path.Combine(_rifeFolderPath, "rife_vs.dll");

        // SVP model path for ONNX files
        var svpModelPath = Path.Combine(_rifeFolderPath, "models");
        var rifeModelDir = Path.Combine(svpModelPath, "rife");

        // Map model names to (modelId, onnxFilename) - validate file exists before TensorRT compilation
        // vsmlrt uses integer model IDs: base versions are 3-digit (e.g., 416), lite versions append 1 (e.g., 4161)
        var (modelId, modelFilename) = options.ModelName switch
        {
            "rife-v4.6" => (46, "rife_v4.6.onnx"),
            "rife-v4.14" => (414, "rife_v4.14.onnx"),
            "rife-v4.14-lite" => (4141, "rife_v4.14_lite.onnx"),
            "rife-v4.15" => (415, "rife_v4.15.onnx"),
            "rife-v4.15-lite" => (4151, "rife_v4.15_lite.onnx"),
            "rife-v4.16" => (416, "rife_v4.16.onnx"),
            "rife-v4.16-lite" => (4161, "rife_v4.16_lite.onnx"),
            "rife-v4.17" => (417, "rife_v4.17.onnx"),
            "rife-v4.18" => (418, "rife_v4.18.onnx"),
            "rife-v4.20" => (420, "rife_v4.20.onnx"),
            "rife-v4.21" => (421, "rife_v4.21.onnx"),
            "rife-v4.22" => (422, "rife_v4.22.onnx"),
            "rife-v4.22-lite" => (4221, "rife_v4.22_lite.onnx"),
            "rife-v4.25" => (425, "rife_v4.25.onnx"),
            "rife-v4.25-lite" => (4251, "rife_v4.25_lite.onnx"),
            "rife-v4.26" => (426, "rife_v4.26.onnx"),
            "rife-UHD" => (49, "rife_v4.9_uhd.onnx"),
            "rife-anime" => (48, "rife_v4.8_anime.onnx"),
            _ => (46, "rife_v4.6.onnx")  // Default to v4.6
        };

        // Validate model file exists before proceeding (avoids 5-15 min TensorRT failure)
        var modelPath = Path.Combine(rifeModelDir, modelFilename);
        if (!File.Exists(modelPath))
        {
            // Try to find what models ARE available
            var availableModels = Directory.Exists(rifeModelDir)
                ? Directory.GetFiles(rifeModelDir, "*.onnx").Select(Path.GetFileName).ToList()
                : [];

            var availableMsg = availableModels.Count > 0
                ? $"Available models: {string.Join(", ", availableModels)}"
                : $"No ONNX models found in {rifeModelDir}";

            throw new FileNotFoundException(
                $"RIFE model not found: {modelFilename}\n" +
                $"Expected at: {modelPath}\n" +
                $"{availableMsg}");
        }

        _logger?.LogDebug($"[RIFE] Using model ID {modelId} for: {modelPath}");

        // Determine engine backend
        var engineBackend = options.Engine switch
        {
            RifeEngine.TensorRT => "Backend.TRT",
            RifeEngine.Vulkan => "Backend.OV_CPU",
            RifeEngine.NCNN => "Backend.NCNN_VK",
            _ => "Backend.TRT"
        };

        var gpuThreads = options.GpuThreads;
        var sceneDetect = options.SceneDetection == SceneChangeDetection.Disabled ? "None" : "True";
        var targetHeight = options.FrameHeight;

        return $@"
import vapoursynth as vs
import sys
import os

core = vs.core

sys.path.insert(0, r'{_rifeFolderPath}')

try:
    bs_plugin = r'C:\Program Files\VapourSynth\plugins\BestSource.dll'
    if os.path.exists(bs_plugin):
        core.std.LoadPlugin(bs_plugin)
except:
    pass

try:
    core.std.LoadPlugin(r'{pluginPath}')
    core.std.LoadPlugin(r'{Path.Combine(_rifeFolderPath, "vstrt.dll")}')
    core.std.LoadPlugin(r'{Path.Combine(_rifeFolderPath, "akarin.dll")}')
except:
    pass

try:
    import vsmlrt
    from vsmlrt import RIFE, Backend
    # Override models_path to use SVP's model location
    vsmlrt.models_path = r'{svpModelPath}'
except ImportError as e:
    raise Exception(f'Failed to import vsmlrt module: {{e}}')

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
                raise Exception('No VapourSynth source plugin found.')

width = clip.width
height = clip.height
fps_num = clip.fps.numerator
fps_den = clip.fps.denominator

target_height = {targetHeight}
if target_height > 0 and target_height != height:
    target_width = int(width * target_height / height)
    target_width = target_width if target_width % 2 == 0 else target_width + 1
    clip = core.resize.Bicubic(clip, width=target_width, height=target_height)
    width = target_width
    height = target_height

clip = core.resize.Bicubic(clip, format=vs.RGBS, matrix_in_s='709')

def pad_to_multiple(dimension, multiple=32):
    remainder = dimension % multiple
    if remainder == 0:
        return dimension
    return dimension + (multiple - remainder)

padded_width = pad_to_multiple(width)
padded_height = pad_to_multiple(height)

if padded_width != width or padded_height != height:
    clip = core.resize.Bicubic(clip, width=padded_width, height=padded_height)

try:
    backend = {engineBackend}(
        num_streams={gpuThreads},
        device_id={options.GpuId}
    )

    # Use integer model ID - vsmlrt only accepts integers, not string paths
    # Model IDs: base versions are 3-digit (e.g., 416), lite versions append 1 (e.g., 4161)
    clip = RIFE(clip, {multiplier}, 1.0, None, None, None, {modelId}, backend, {(options.TtaMode ? "True" : "False")}, {(options.UhdMode ? "True" : "False")}, {sceneDetect})

except Exception as e:
    import traceback
    error_msg = f'RIFE interpolation failed: {{str(e)}}'
    print(error_msg, file=sys.stderr)
    traceback.print_exc()
    raise

if padded_width != width or padded_height != height:
    clip = core.resize.Bicubic(clip, width=width, height=height)

clip = core.resize.Bicubic(clip, format=vs.YUV420P8, matrix_s='709')

clip.set_output()
";
    }

    /// <summary>
    /// Check if RIFE is available and properly configured
    /// </summary>
    public bool IsRifeAvailable()
    {
        try
        {
            EnsureValidated();

            var pythonCheck = new SysProcess
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _pythonPath,
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            pythonCheck.Start();
            pythonCheck.WaitForExit(5000);

            if (pythonCheck.ExitCode != 0)
            {
                _logger?.LogDebug("Python not found or not working");
                return false;
            }

            return true;
        }
        catch
        {
            return false;
        }
    }
}
