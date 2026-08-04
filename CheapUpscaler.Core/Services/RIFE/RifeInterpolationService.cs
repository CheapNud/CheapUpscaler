using SysProcess = System.Diagnostics.Process;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using CheapHelpers.MediaProcessing.Services.Utilities;
using CheapUpscaler.Core.Services.VapourSynth;
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
    /// <summary>
    /// Single source of truth for the RIFE models we support: our model name, SVP's ONNX
    /// filename, and the integer model ID vsmlrt expects (base versions are 3-digit, e.g. 416;
    /// lite versions append 1, e.g. 4161).
    /// </summary>
    private sealed record RifeModel(string Name, string OnnxFile, int ModelId);

    private static readonly RifeModel[] Models =
    [
        new("rife-v4.6", "rife_v4.6.onnx", 46),
        new("rife-v4.14", "rife_v4.14.onnx", 414),
        new("rife-v4.14-lite", "rife_v4.14_lite.onnx", 4141),
        new("rife-v4.15", "rife_v4.15.onnx", 415),
        new("rife-v4.15-lite", "rife_v4.15_lite.onnx", 4151),
        new("rife-v4.16", "rife_v4.16.onnx", 416),
        new("rife-v4.16-lite", "rife_v4.16_lite.onnx", 4161),
        new("rife-v4.17", "rife_v4.17.onnx", 417),
        new("rife-v4.18", "rife_v4.18.onnx", 418),
        new("rife-v4.20", "rife_v4.20.onnx", 420),
        new("rife-v4.21", "rife_v4.21.onnx", 421),
        new("rife-v4.22", "rife_v4.22.onnx", 422),
        new("rife-v4.22-lite", "rife_v4.22_lite.onnx", 4221),
        new("rife-v4.25", "rife_v4.25.onnx", 425),
        new("rife-v4.25-lite", "rife_v4.25_lite.onnx", 4251),
        new("rife-v4.26", "rife_v4.26.onnx", 426),
        new("rife-anime", "rife_v4.8_anime.onnx", 48),
        new("rife-UHD", "rife_v4.9_uhd.onnx", 49)
    ];

    private readonly string _rifeFolderPath;
    private readonly string _pythonPath;
    private readonly IVapourSynthEnvironment _environment;
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

        // Check for ONNX files (TensorRT) - case-insensitive
        var hasOnnx = Directory.EnumerateFiles(rifeModelDir)
            .Any(f => f.EndsWith(".onnx", StringComparison.OrdinalIgnoreCase));
        if (hasOnnx)
        {
            _logger?.LogDebug("[RIFE] ONNX models found, selecting TensorRT engine");
            return RifeEngine.TensorRT;
        }

        // Check for NCNN files (.bin and .param pairs) - case-insensitive
        var hasBin = Directory.EnumerateFiles(rifeModelDir)
            .Any(f => f.EndsWith(".bin", StringComparison.OrdinalIgnoreCase));
        var hasParam = Directory.EnumerateFiles(rifeModelDir)
            .Any(f => f.EndsWith(".param", StringComparison.OrdinalIgnoreCase));
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

        // Check for ONNX files (TensorRT) - case-insensitive
        if (Directory.EnumerateFiles(rifeModelDir).Any(f => f.EndsWith(".onnx", StringComparison.OrdinalIgnoreCase)))
            engines.Add(RifeEngine.TensorRT);

        // Check for NCNN files - case-insensitive
        if (Directory.EnumerateFiles(rifeModelDir).Any(f => f.EndsWith(".bin", StringComparison.OrdinalIgnoreCase)) &&
            Directory.EnumerateFiles(rifeModelDir).Any(f => f.EndsWith(".param", StringComparison.OrdinalIgnoreCase)))
            engines.Add(RifeEngine.NCNN);

        return engines;
    }

    /// <summary>
    /// Maps ONNX filename (without extension) to our model name format
    /// e.g., "rife_v4.6" -> "rife-v4.6", "rife_v4.22_lite" -> "rife-v4.22-lite"
    /// </summary>
    private static string? MapOnnxFilenameToModelName(string onnxName)
    {
        return Models
            .FirstOrDefault(m => Path.GetFileNameWithoutExtension(m.OnnxFile)
                .Equals(onnxName, StringComparison.OrdinalIgnoreCase))?.Name;
    }

    public RifeInterpolationService(
        string rifeFolderPath = "",
        string pythonPath = "",
        ILogger<RifeInterpolationService>? logger = null,
        IVapourSynthEnvironment? environment = null)
    {
        _rifeFolderPath = rifeFolderPath;
        _logger = logger;
        // Falls back to a standalone environment for hosts that construct this service directly.
        _environment = environment ?? new VapourSynthEnvironment();

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
            using var process = new SysProcess
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = pythonCommand,
                    Arguments = "--version",
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            if (!process.WaitForExit(2000))
            {
                try { process.Kill(); } catch { }
                return false;
            }
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
            var requiredFiles = new[] { "rife.dll", "rife_vs.dll", "vsmlrt.py", "vstrt.dll" };
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
            var vspipePath = _environment.VsPipePath;
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

                _logger?.LogDebug("Created VapourSynth script: {ScriptPath}", tempScriptPath);

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
                                _logger?.LogDebug("[vspipe stdout] {Line}", line);
                                outputBuilder.AppendLine(line);
                            }
                        }, cancellationToken);

                        var stderrTask = Task.Run(async () =>
                        {
                            string? line;
                            while ((line = await test.StandardError.ReadLineAsync(cancellationToken)) != null)
                            {
                                _logger?.LogDebug("[vspipe stderr] {Line}", line);
                                errorBuilder.AppendLine(line);
                            }
                        }, cancellationToken);

                        // Wait up to 20 minutes for TensorRT to compile on first run (async - never block a threadpool thread)
                        using var testTimeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                        testTimeoutCts.CancelAfter(TimeSpan.FromMinutes(20));
                        try
                        {
                            await test.WaitForExitAsync(testTimeoutCts.Token);
                        }
                        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                        {
                            _logger?.LogWarning("VapourSynth script test timed out after 20 minutes");
                            try { test.Kill(entireProcessTree: true); } catch { }
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

                        _logger?.LogDebug("VapourSynth script test passed. Output: {TestOutput}", testOutput);
                    }
                }

                // Run the actual processing through the shared vspipe -> FFmpeg pipeline
                // (handles progress, cancellation, orphan-kill and audio/subtitle muxing)
                var ffmpegExe = VspipePipeline.ResolveFfmpegPath(ffmpegPath);
                var encodeArgs = VspipePipeline.BuildEncodeArguments(inputVideoPath, outputVideoPath, preset: "fast", ffmpegPath: ffmpegExe);

                var (success, _, _) = await VspipePipeline.RunAsync(
                    vspipePath, tempScriptPath, ffmpegExe, encodeArgs, progress, _logger, cancellationToken);

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

        _logger?.LogDebug("Starting RIFE interpolation: {PythonPath} {Arguments}", _pythonPath, arguments);

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

                _logger?.LogDebug("[RIFE] {Data}", e.Data);

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
    /// Get all supported RIFE model names (static list of known models).
    /// Use GetAvailableModels() instance method to get actually installed models.
    /// </summary>
    public static string[] GetSupportedModels() => Models.Select(m => m.Name).ToArray();

    /// <summary>
    /// Generate a VapourSynth script for SVP RIFE processing.
    /// When scene detection is enabled, misc.SCDetect tags cuts on the source clip so vsmlrt's RIFE
    /// repeats the frame across a cut instead of interpolating between two unrelated shots.
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

        // Resolve the model from the shared table - validate the file exists before TensorRT compilation
        var model = Models.FirstOrDefault(m => m.Name == options.ModelName) ?? Models[0]; // Default to v4.6
        var (modelId, modelFilename) = (model.ModelId, model.OnnxFile);

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

        _logger?.LogDebug("[RIFE] Using model ID {ModelId} for: {ModelPath}", modelId, modelPath);

        // Determine engine backend
        var engineBackend = options.Engine switch
        {
            RifeEngine.TensorRT => "Backend.TRT",
            RifeEngine.Vulkan => "Backend.OV_CPU",
            RifeEngine.NCNN => "Backend.NCNN_VK",
            _ => "Backend.TRT"
        };

        var gpuThreads = options.GpuThreads;
        var targetHeight = options.FrameHeight;

        // vsmlrt's RIFE has no scene-change parameter. It looks for the _SceneChangeNext frame
        // prop and, when set, repeats the source frame instead of interpolating (akarin.Select,
        // or std.FrameEval as fallback). That prop has to be produced up-front by misc.SCDetect.
        // 0.1 is the MiscFilters default and the value vs-mlrt's own docs use.
        const string SceneChangeThreshold = "0.1";
        var sceneDetectSnippet = options.SceneDetection == SceneChangeDetection.Disabled
            ? ""
            : $@"
try:
    clip = core.misc.SCDetect(clip, threshold={SceneChangeThreshold})
except Exception as e:
    print(f'[RIFE] Scene change detection unavailable ({{e}}). Interpolation will ghost across cuts - install the VapourSynth MiscFilters plugin (misc.SCDetect).', file=sys.stderr)
";

        return $@"
import vapoursynth as vs
import sys
import os

core = vs.core

sys.path.insert(0, {VspipePipeline.PyQuote(_rifeFolderPath)})

try:
    bs_plugin = r'C:\Program Files\VapourSynth\plugins\BestSource.dll'
    if os.path.exists(bs_plugin):
        core.std.LoadPlugin(bs_plugin)
except:
    pass

try:
    core.std.LoadPlugin({VspipePipeline.PyQuote(pluginPath)})
    core.std.LoadPlugin({VspipePipeline.PyQuote(Path.Combine(_rifeFolderPath, "vstrt.dll"))})
    core.std.LoadPlugin({VspipePipeline.PyQuote(Path.Combine(_rifeFolderPath, "akarin.dll"))})
except:
    pass

try:
    import vsmlrt
    from vsmlrt import RIFE, Backend
    # Override models_path to use SVP's model location
    vsmlrt.models_path = {VspipePipeline.PyQuote(svpModelPath)}
except ImportError as e:
    raise Exception(f'Failed to import vsmlrt module: {{e}}')

_source = {VspipePipeline.PyQuote(inputVideoPath)}
try:
    clip = core.bs.VideoSource(source=_source)
except:
    try:
        clip = core.ffms2.Source(_source)
    except:
        try:
            clip = core.lsmas.LWLibavSource(_source)
        except:
            try:
                clip = core.avisource.AVISource(_source)
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
{VspipePipeline.MatrixDetectSnippet}{sceneDetectSnippet}
clip = core.resize.Bicubic(clip, format=vs.RGBS, matrix_in=_matrix)

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
    # Keyword args only: vsmlrt.RIFE's positional order is
    # (clip, multi, scale, tiles, tilesize, overlap, model, backend, ensemble, video_player, _implementation)
    # - it has no uhd or scene-detect parameter (scene changes come from the _SceneChangeNext prop above).
    clip = RIFE(clip, multi={multiplier}, scale=1.0, model={modelId}, backend=backend, ensemble={(options.TtaMode ? "True" : "False")})

except Exception as e:
    import traceback
    error_msg = f'RIFE interpolation failed: {{str(e)}}'
    print(error_msg, file=sys.stderr)
    traceback.print_exc()
    raise

if padded_width != width or padded_height != height:
    clip = core.resize.Bicubic(clip, width=width, height=height)

clip = core.resize.Bicubic(clip, format=vs.YUV420P8, matrix=_matrix)

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
