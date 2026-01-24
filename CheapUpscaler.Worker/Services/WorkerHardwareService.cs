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
    public Task<HardwareCapabilities> DetectHardwareAsync()
    {
        try
        {
            var capabilities = new HardwareCapabilities
            {
                CpuName = GetCpuInfo(),
                CpuCoreCount = Environment.ProcessorCount,
                AvailableGpus = DetectGpusInContainer(),
                // In Docker, these typically aren't available unless using NVIDIA container runtime
                HasNvidiaGpu = CheckNvidiaInContainer(),
                NvencAvailable = false, // Would need nvidia-smi access
                IsIntelCpu = Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER")?.Contains("Intel") ?? false
            };

            // Set primary GPU name if available
            if (capabilities.AvailableGpus.Count > 0)
            {
                capabilities.GpuName = capabilities.AvailableGpus[0];
            }

            logger.LogInformation("Hardware detected: CPU={CpuName}, Cores={Cores}, NVIDIA={HasNvidia}",
                capabilities.CpuName, capabilities.CpuCoreCount, capabilities.HasNvidiaGpu);

            return Task.FromResult(capabilities);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error detecting hardware");
            return Task.FromResult(new HardwareCapabilities
            {
                CpuName = "Unknown",
                CpuCoreCount = Environment.ProcessorCount
            });
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
}
