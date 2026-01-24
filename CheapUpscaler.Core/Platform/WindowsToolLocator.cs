#if WINDOWS
using System.Diagnostics;
using System.Text.RegularExpressions;
using CheapHelpers.MediaProcessing.Services;
using CheapUpscaler.Shared.Platform;
using Microsoft.Extensions.Logging;

namespace CheapUpscaler.Core.Platform;

/// <summary>
/// Windows-specific tool detection using CheapHelpers.MediaProcessing
/// Wraps existing detection services for consistent interface
/// </summary>
public class WindowsToolLocator(
    SvpDetectionService svpDetection,
    HardwareDetectionService hardwareDetection,
    ExecutableDetectionService executableDetection,
    ILogger<WindowsToolLocator>? logger = null) : IToolLocator
{
    private readonly WindowsPlatformPaths _paths = new();

    public async Task<ToolInfo?> DetectPythonAsync(CancellationToken cancellationToken = default)
    {
        // First check SVP's bundled Python
        var svp = svpDetection.DetectSvpInstallation();
        if (svp.IsInstalled && !string.IsNullOrEmpty(svp.PythonPath) && File.Exists(svp.PythonPath))
        {
            var version = await GetCommandVersionAsync(svp.PythonPath, "--version", @"Python ([\d\.]+)", cancellationToken);
            logger?.LogDebug("Detected SVP Python: {Path}", svp.PythonPath);
            return new ToolInfo(svp.PythonPath, version);
        }

        // Fall back to system Python using PATH enumeration
        var pythonPath = executableDetection.GetExecutablePathFromCommand("python");
        if (pythonPath != null && File.Exists(pythonPath))
        {
            var version = await GetCommandVersionAsync(pythonPath, "--version", @"Python ([\d\.]+)", cancellationToken);
            logger?.LogDebug("Detected system Python: {Path}", pythonPath);
            return new ToolInfo(pythonPath, version);
        }

        // Also try python3
        pythonPath = executableDetection.GetExecutablePathFromCommand("python3");
        if (pythonPath != null && File.Exists(pythonPath))
        {
            var version = await GetCommandVersionAsync(pythonPath, "--version", @"Python ([\d\.]+)", cancellationToken);
            logger?.LogDebug("Detected system Python3: {Path}", pythonPath);
            return new ToolInfo(pythonPath, version);
        }

        return null;
    }

    public async Task<ToolInfo?> DetectVspipeAsync(CancellationToken cancellationToken = default)
    {
        // Check known paths first
        foreach (var searchPath in _paths.GetVspipeSearchPaths())
        {
            var vspipePath = Path.Combine(searchPath, "vspipe.exe");
            if (File.Exists(vspipePath))
            {
                var version = await GetCommandVersionAsync(vspipePath, "--version", @"(R\d+)", cancellationToken);
                logger?.LogDebug("Detected vspipe: {Path}", vspipePath);
                return new ToolInfo(vspipePath, version);
            }
        }

        // Try PATH
        var pathResult = executableDetection.GetExecutablePathFromCommand("vspipe");
        if (pathResult != null && File.Exists(pathResult))
        {
            var version = await GetCommandVersionAsync(pathResult, "--version", @"(R\d+)", cancellationToken);
            return new ToolInfo(pathResult, version);
        }

        return null;
    }

    public async Task<ToolInfo?> DetectFFmpegAsync(CancellationToken cancellationToken = default)
    {
        // Use ExecutableDetectionService which already handles SVP + PATH + common locations
        var ffmpegPath = executableDetection.DetectFFmpeg(useSvpEncoders: true, customPath: null);
        if (ffmpegPath != null)
        {
            var version = await GetCommandVersionAsync(ffmpegPath, "-version", @"ffmpeg version ([\d\.\-n]+)", cancellationToken);
            logger?.LogDebug("Detected FFmpeg: {Path}", ffmpegPath);
            return new ToolInfo(ffmpegPath, version);
        }

        return null;
    }

    public async Task<ToolInfo?> DetectFFprobeAsync(CancellationToken cancellationToken = default)
    {
        // Use ExecutableDetectionService
        var ffprobePath = executableDetection.DetectFFprobe(useSvpEncoders: true, customPath: null);
        if (ffprobePath != null)
        {
            var version = await GetCommandVersionAsync(ffprobePath, "-version", @"ffprobe version ([\d\.\-n]+)", cancellationToken);
            logger?.LogDebug("Detected FFprobe: {Path}", ffprobePath);
            return new ToolInfo(ffprobePath, version);
        }

        return null;
    }

    public async Task<bool> IsCudaAvailableAsync(CancellationToken cancellationToken = default)
    {
        var hardware = await hardwareDetection.DetectHardwareAsync();
        return hardware.HasNvidiaGpu;
    }

    public async Task<bool> IsTensorRtAvailableAsync(CancellationToken cancellationToken = default)
    {
        // Check if TensorRT Python module is available
        try
        {
            var pythonInfo = await DetectPythonAsync(cancellationToken);
            if (pythonInfo == null) return false;

            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = pythonInfo.Path,
                    Arguments = "-c \"import tensorrt; print(tensorrt.__version__)\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            await process.WaitForExitAsync(cancellationToken);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    public async Task<GpuInfo?> GetGpuInfoAsync(CancellationToken cancellationToken = default)
    {
        var hardware = await hardwareDetection.DetectHardwareAsync();
        if (string.IsNullOrEmpty(hardware.GpuName))
        {
            return null;
        }

        var tensorRtAvailable = await IsTensorRtAvailableAsync(cancellationToken);

        return new GpuInfo(
            hardware.GpuName,
            VramMB: null, // HardwareCapabilities doesn't expose VRAM
            NvencAvailable: hardware.NvencAvailable,
            CudaAvailable: hardware.HasNvidiaGpu,
            TensorRtAvailable: tensorRtAvailable,
            DeviceIndex: 0);
    }

    public async Task<IReadOnlyList<GpuInfo>> GetAllGpusAsync(CancellationToken cancellationToken = default)
    {
        var hardware = await hardwareDetection.DetectHardwareAsync();
        var tensorRtAvailable = await IsTensorRtAvailableAsync(cancellationToken);

        var gpus = new List<GpuInfo>();
        var index = 0;
        foreach (var gpuName in hardware.AvailableGpus)
        {
            var isNvidia = gpuName.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase);
            gpus.Add(new GpuInfo(
                gpuName,
                VramMB: null,
                NvencAvailable: isNvidia && hardware.NvencAvailable,
                CudaAvailable: isNvidia,
                TensorRtAvailable: isNvidia && tensorRtAvailable,
                DeviceIndex: index++));
        }

        return gpus;
    }

    private static async Task<string?> GetCommandVersionAsync(string command, string args, string pattern, CancellationToken cancellationToken)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = command,
                    Arguments = args,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            var error = await process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            var combinedOutput = !string.IsNullOrWhiteSpace(output) ? output : error;
            if (!string.IsNullOrWhiteSpace(combinedOutput))
            {
                var match = Regex.Match(combinedOutput, pattern);
                return match.Success ? match.Groups[1].Value : combinedOutput.Split('\n')[0].Trim();
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch { }

        return null;
    }
}
#endif
