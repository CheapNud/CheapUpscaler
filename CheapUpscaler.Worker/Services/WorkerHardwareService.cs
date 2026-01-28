using CheapHelpers.MediaProcessing.Models;
using CheapUpscaler.Components.Services;
using Microsoft.Extensions.Logging;

namespace CheapUpscaler.Worker.Services;

/// <summary>
/// Hardware detection service for Docker/headless environments.
/// Provides basic system info since GPU detection may not work in containers.
/// </summary>
public class WorkerHardwareService(ILogger<WorkerHardwareService> logger) : IHardwareService
{
    public async Task<HardwareCapabilities> DetectHardwareAsync()
    {
        try
        {
            var hasNvidia = CheckNvidiaInContainer();
            var nvencAvailable = hasNvidia && await CheckNvencAvailableAsync();

            var capabilities = new HardwareCapabilities
            {
                CpuName = GetCpuInfo(),
                CpuCoreCount = Environment.ProcessorCount,
                AvailableGpus = DetectGpusInContainer(),
                HasNvidiaGpu = hasNvidia,
                NvencAvailable = nvencAvailable,
                IsIntelCpu = Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER")?.Contains("Intel") ?? false
            };

            // Set primary GPU name if available
            if (capabilities.AvailableGpus.Count > 0)
            {
                capabilities.GpuName = capabilities.AvailableGpus[0];
            }

            logger.LogInformation("Hardware detected: CPU={CpuName}, Cores={Cores}, NVIDIA={HasNvidia}, NVENC={Nvenc}",
                capabilities.CpuName, capabilities.CpuCoreCount, capabilities.HasNvidiaGpu, capabilities.NvencAvailable);

            return capabilities;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error detecting hardware");
            return new HardwareCapabilities
            {
                CpuName = "Unknown",
                CpuCoreCount = Environment.ProcessorCount
            };
        }
    }

    private static string GetCpuInfo()
    {
        // On Linux, try to read /proc/cpuinfo
        if (OperatingSystem.IsLinux() && File.Exists("/proc/cpuinfo"))
        {
            try
            {
                var lines = File.ReadAllLines("/proc/cpuinfo");
                var modelLine = lines.FirstOrDefault(l => l.StartsWith("model name"));
                if (modelLine != null)
                {
                    var parts = modelLine.Split(':');
                    if (parts.Length > 1)
                    {
                        return parts[1].Trim();
                    }
                }
            }
            catch
            {
                // Ignore errors reading cpuinfo
            }
        }

        // Fallback
        return Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? $"{Environment.ProcessorCount}-core CPU";
    }

    private static List<string> DetectGpusInContainer()
    {
        var gpus = new List<string>();

        // Check for NVIDIA container runtime
        var nvidiaVisible = Environment.GetEnvironmentVariable("NVIDIA_VISIBLE_DEVICES");
        if (!string.IsNullOrEmpty(nvidiaVisible) && nvidiaVisible != "void")
        {
            gpus.Add($"NVIDIA GPU (Container: {nvidiaVisible})");
        }

        // Check for CUDA device order (set by nvidia-container-toolkit)
        var cudaDevices = Environment.GetEnvironmentVariable("CUDA_VISIBLE_DEVICES");
        if (!string.IsNullOrEmpty(cudaDevices))
        {
            if (!gpus.Any(g => g.Contains("NVIDIA")))
            {
                gpus.Add($"NVIDIA CUDA Device(s): {cudaDevices}");
            }
        }

        return gpus;
    }

    private static bool CheckNvidiaInContainer()
    {
        // Check for NVIDIA container runtime environment variables
        var nvidiaVisible = Environment.GetEnvironmentVariable("NVIDIA_VISIBLE_DEVICES");
        if (!string.IsNullOrEmpty(nvidiaVisible) && nvidiaVisible != "void")
        {
            return true;
        }

        // Check if /dev/nvidia0 exists (GPU device in container)
        if (OperatingSystem.IsLinux() && File.Exists("/dev/nvidia0"))
        {
            return true;
        }

        return false;
    }

    private async Task<bool> CheckNvencAvailableAsync()
    {
        try
        {
            // Try ffmpeg encoder check first - most reliable
            var ffmpegResult = await RunCommandAsync("ffmpeg", "-hide_banner -encoders 2>&1 | grep -i nvenc");
            if (!string.IsNullOrEmpty(ffmpegResult) && ffmpegResult.Contains("nvenc", StringComparison.OrdinalIgnoreCase))
            {
                logger.LogInformation("NVENC detected via ffmpeg encoders");
                return true;
            }

            // Fallback: check nvidia-smi for encoder capability
            var nvidiaSmiResult = await RunCommandAsync("nvidia-smi", "--query-gpu=encoder.stats.averageFps --format=csv,noheader");
            if (nvidiaSmiResult != null && !nvidiaSmiResult.Contains("error", StringComparison.OrdinalIgnoreCase))
            {
                logger.LogInformation("NVENC detected via nvidia-smi");
                return true;
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Error checking NVENC availability");
        }

        return false;
    }

    private static async Task<string?> RunCommandAsync(string command, string arguments)
    {
        try
        {
            using var process = new System.Diagnostics.Process();
            process.StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/sh",
                Arguments = OperatingSystem.IsWindows() ? $"/c {command} {arguments}" : $"-c \"{command} {arguments}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            return output;
        }
        catch
        {
            return null;
        }
    }
}
