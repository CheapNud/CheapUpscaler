using CheapHelpers.MediaProcessing.Models;
using Microsoft.Extensions.Logging;

namespace CheapUpscaler.Core.Services.RIFE;

/// <summary>
/// Detects and recommends the optimal RIFE variant based on hardware capabilities
/// Supports both rife-ncnn-vulkan (Vulkan) and rife-tensorrt (TensorRT)
/// </summary>
public class RifeVariantDetector
{
    /// <summary>
    /// Detect recommended RIFE variant based on hardware capabilities
    /// TensorRT is recommended for RTX GPUs (2000, 3000, 4000, 5000 series)
    /// Vulkan is recommended for all other GPUs
    /// </summary>
    public static string DetectRecommendedVariant(HardwareCapabilities hardwareCapabilities, ILogger? logger = null)
    {
        // RTX GPUs (2000, 3000, 4000, 5000 series) support TensorRT
        if (hardwareCapabilities.NvencAvailable && IsRtxGpu(hardwareCapabilities.GpuName))
        {
            logger?.LogDebug($"Detected RTX GPU: {hardwareCapabilities.GpuName}");
            logger?.LogDebug("Recommended RIFE variant: TensorRT (faster performance on RTX GPUs)");
            return "TensorRT";
        }

        // Fallback to Vulkan for other GPUs (GTX, AMD, Intel, etc.)
        logger?.LogDebug($"Detected GPU: {hardwareCapabilities.GpuName}");
        logger?.LogDebug("Recommended RIFE variant: Vulkan (universal compatibility)");
        return "Vulkan";
    }

    /// <summary>
    /// Check if GPU is an RTX model that supports TensorRT
    /// </summary>
    private static bool IsRtxGpu(string gpuName)
    {
        if (string.IsNullOrEmpty(gpuName))
            return false;

        // Check for RTX 20-series (2060, 2070, 2080, etc.)
        if (gpuName.Contains("RTX 20", StringComparison.OrdinalIgnoreCase))
            return true;

        // Check for RTX 30-series (3060, 3070, 3080, 3090, etc.)
        if (gpuName.Contains("RTX 30", StringComparison.OrdinalIgnoreCase))
            return true;

        // Check for RTX 40-series (4060, 4070, 4080, 4090, etc.)
        if (gpuName.Contains("RTX 40", StringComparison.OrdinalIgnoreCase))
            return true;

        // Check for RTX 50-series (future-proofing)
        if (gpuName.Contains("RTX 50", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    /// <summary>
    /// Get description of RIFE variant
    /// </summary>
    public static string GetVariantDescription(string variant)
    {
        return variant switch
        {
            "TensorRT" => "TensorRT (Optimized for RTX GPUs - faster performance)",
            "Vulkan" => "Vulkan (Universal compatibility - works on most GPUs)",
            _ => "Unknown variant"
        };
    }

    /// <summary>
    /// Get executable name for variant
    /// </summary>
    public static string GetExecutableName(string variant)
    {
        return variant switch
        {
            "TensorRT" => "rife-tensorrt.exe",
            "Vulkan" => "rife-ncnn-vulkan.exe",
            _ => "rife-ncnn-vulkan.exe" // Default to Vulkan
        };
    }

    /// <summary>
    /// Validate that RIFE executable exists for the given variant
    /// </summary>
    public static bool ValidateVariantExecutable(string variant, string searchPath = ".", ILogger? logger = null)
    {
        var executableName = GetExecutableName(variant);
        var fullPath = Path.Combine(searchPath, executableName);

        if (File.Exists(fullPath))
        {
            logger?.LogDebug($"Found RIFE executable: {fullPath}");
            return true;
        }

        logger?.LogDebug($"RIFE executable not found: {fullPath}");
        return false;
    }

    /// <summary>
    /// Auto-detect available RIFE variants in a directory
    /// Returns list of available variants
    /// </summary>
    public static List<string> DetectAvailableVariants(string searchPath = ".", ILogger? logger = null)
    {
        var availableVariants = new List<string>();

        // Check for TensorRT variant
        if (File.Exists(Path.Combine(searchPath, "rife-tensorrt.exe")))
        {
            availableVariants.Add("TensorRT");
            logger?.LogDebug("Found RIFE TensorRT variant");
        }

        // Check for Vulkan variant
        if (File.Exists(Path.Combine(searchPath, "rife-ncnn-vulkan.exe")))
        {
            availableVariants.Add("Vulkan");
            logger?.LogDebug("Found RIFE Vulkan variant");
        }

        if (availableVariants.Count == 0)
        {
            logger?.LogDebug("No RIFE executables found in search path");
        }

        return availableVariants;
    }
}
