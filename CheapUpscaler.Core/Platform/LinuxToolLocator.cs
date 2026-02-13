using System.Diagnostics;
using System.Text.RegularExpressions;
using CheapUpscaler.Shared.Platform;
using Microsoft.Extensions.Logging;

namespace CheapUpscaler.Core.Platform;

/// <summary>
/// Linux-specific tool detection using native commands
/// Designed for both native Linux and Docker container environments
/// </summary>
public class LinuxToolLocator(ILogger<LinuxToolLocator>? logger = null) : IToolLocator
{
    private readonly LinuxPlatformPaths _paths = new();

    public async Task<ToolInfo?> DetectPythonAsync(CancellationToken cancellationToken = default)
    {
        // Try python3 first (standard on modern Linux)
        var pythonPath = await _paths.ResolveCommandPathAsync("python3", cancellationToken)
                      ?? await _paths.ResolveCommandPathAsync("python", cancellationToken);

        if (pythonPath == null)
        {
            // Check known paths
            foreach (var searchPath in _paths.GetPythonSearchPaths())
            {
                var python3 = Path.Combine(searchPath, "python3");
                var python = Path.Combine(searchPath, "python");

                if (File.Exists(python3))
                {
                    pythonPath = python3;
                    break;
                }
                if (File.Exists(python))
                {
                    pythonPath = python;
                    break;
                }
            }
        }

        if (pythonPath != null)
        {
            var version = await GetCommandVersionAsync(pythonPath, "--version", @"Python ([\d\.]+)", cancellationToken);
            logger?.LogDebug("Detected Python: {Path} v{Version}", pythonPath, version);
            return new ToolInfo(pythonPath, version);
        }

        return null;
    }

    public async Task<ToolInfo?> DetectVspipeAsync(CancellationToken cancellationToken = default)
    {
        var vspipePath = await _paths.ResolveCommandPathAsync("vspipe", cancellationToken);

        if (vspipePath == null)
        {
            foreach (var searchPath in _paths.GetVspipeSearchPaths())
            {
                var candidate = Path.Combine(searchPath, "vspipe");
                if (File.Exists(candidate))
                {
                    vspipePath = candidate;
                    break;
                }
            }
        }

        if (vspipePath != null)
        {
            var version = await GetCommandVersionAsync(vspipePath, "--version", @"(R\d+)", cancellationToken);
            logger?.LogDebug("Detected vspipe: {Path} v{Version}", vspipePath, version);
            return new ToolInfo(vspipePath, version);
        }

        return null;
    }

    public async Task<ToolInfo?> DetectFFmpegAsync(CancellationToken cancellationToken = default)
    {
        var ffmpegPath = await _paths.ResolveCommandPathAsync("ffmpeg", cancellationToken);

        if (ffmpegPath == null)
        {
            foreach (var searchPath in _paths.GetFFmpegSearchPaths())
            {
                var candidate = Path.Combine(searchPath, "ffmpeg");
                if (File.Exists(candidate))
                {
                    ffmpegPath = candidate;
                    break;
                }
            }
        }

        if (ffmpegPath != null)
        {
            var version = await GetCommandVersionAsync(ffmpegPath, "-version", @"ffmpeg version ([\d\.\-n]+)", cancellationToken);
            logger?.LogDebug("Detected FFmpeg: {Path} v{Version}", ffmpegPath, version);
            return new ToolInfo(ffmpegPath, version);
        }

        return null;
    }

    public async Task<ToolInfo?> DetectFFprobeAsync(CancellationToken cancellationToken = default)
    {
        var ffprobePath = await _paths.ResolveCommandPathAsync("ffprobe", cancellationToken);

        if (ffprobePath == null)
        {
            foreach (var searchPath in _paths.GetFFmpegSearchPaths())
            {
                var candidate = Path.Combine(searchPath, "ffprobe");
                if (File.Exists(candidate))
                {
                    ffprobePath = candidate;
                    break;
                }
            }
        }

        if (ffprobePath != null)
        {
            var version = await GetCommandVersionAsync(ffprobePath, "-version", @"ffprobe version ([\d\.\-n]+)", cancellationToken);
            logger?.LogDebug("Detected FFprobe: {Path} v{Version}", ffprobePath, version);
            return new ToolInfo(ffprobePath, version);
        }

        return null;
    }

    public async Task<bool> IsCudaAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "nvidia-smi",
                    Arguments = "--query-gpu=name --format=csv,noheader",
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

    public async Task<bool> IsTensorRtAvailableAsync(CancellationToken cancellationToken = default)
    {
        // Check if TensorRT libraries are available
        var tensorrtPaths = new[]
        {
            "/usr/lib/x86_64-linux-gnu/libnvinfer.so",
            "/usr/local/lib/libnvinfer.so",
            "/opt/tensorrt/lib/libnvinfer.so"
        };

        foreach (var path in tensorrtPaths)
        {
            if (File.Exists(path))
            {
                return true;
            }
        }

        // Also try via Python
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
        // Use nvidia-smi for NVIDIA GPU detection
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "nvidia-smi",
                    Arguments = "--query-gpu=name,memory.total --format=csv,noheader,nounits",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
            {
                var parts = output.Trim().Split(',');
                if (parts.Length >= 2)
                {
                    var name = parts[0].Trim();
                    var vramMB = long.TryParse(parts[1].Trim(), out var vram) ? vram : (long?)null;

                    var tensorRtAvailable = await IsTensorRtAvailableAsync(cancellationToken);

                    logger?.LogDebug("Detected GPU: {Name} with {VramMB}MB", name, vramMB);
                    return new GpuInfo(
                        name,
                        vramMB,
                        NvencAvailable: true,
                        CudaAvailable: true,
                        TensorRtAvailable: tensorRtAvailable,
                        DeviceIndex: 0);
                }
            }
        }
        catch (Exception ex)
        {
            logger?.LogDebug("nvidia-smi not available: {Message}", ex.Message);
        }

        return null;
    }

    public async Task<IReadOnlyList<GpuInfo>> GetAllGpusAsync(CancellationToken cancellationToken = default)
    {
        var gpus = new List<GpuInfo>();

        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "nvidia-smi",
                    Arguments = "--query-gpu=index,name,memory.total --format=csv,noheader,nounits",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
            {
                var tensorRtAvailable = await IsTensorRtAvailableAsync(cancellationToken);

                foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    var parts = line.Trim().Split(',');
                    if (parts.Length >= 3)
                    {
                        var index = int.TryParse(parts[0].Trim(), out var i) ? i : 0;
                        var name = parts[1].Trim();
                        var vramMB = long.TryParse(parts[2].Trim(), out var vram) ? vram : (long?)null;

                        gpus.Add(new GpuInfo(
                            name,
                            vramMB,
                            NvencAvailable: true,
                            CudaAvailable: true,
                            TensorRtAvailable: tensorRtAvailable,
                            DeviceIndex: index));
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger?.LogDebug("Failed to enumerate GPUs: {Message}", ex.Message);
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
            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            var output = await outputTask;
            var error = await errorTask;

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
