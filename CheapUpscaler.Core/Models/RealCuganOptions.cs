namespace CheapUpscaler.Core.Models;

/// <summary>
/// Configuration options for Real-CUGAN AI upscaling
/// Uses VapourSynth + vs-mlrt with TensorRT backend (10-13x faster than Real-ESRGAN)
/// Real-CUGAN is optimized for anime/cartoon content
/// </summary>
public class RealCuganOptions
{
    // === Core Engine Settings ===

    /// <summary>
    /// Denoising level for upscaling
    /// -1 = No denoising effect (pure upscaling)
    /// 0 = Conservative denoising
    /// 1 = Light denoising (only for 2x scale)
    /// 2 = Medium denoising (only for 2x scale)
    /// 3 = Aggressive denoising
    /// </summary>
    public int Noise { get; set; } = -1;

    /// <summary>
    /// Upscaling factor (2x, 3x, or 4x)
    /// 2x: 720p -> 1440p, 1080p -> 2160p (supports all noise levels)
    /// 3x: 720p -> 2160p (noise 0, 3 only)
    /// 4x: 540p -> 2160p (noise 0, 3 only)
    /// </summary>
    public int Scale { get; set; } = 2;

    /// <summary>
    /// GPU device ID to use for processing
    /// 0 = First GPU, 1 = Second GPU, etc.
    /// </summary>
    public int GpuId { get; set; } = 0;

    // === Performance Settings ===

    /// <summary>
    /// Use FP16 (half precision) for faster processing
    /// True = ~50% faster with minimal quality loss
    /// False = Full precision (FP32)
    /// Recommended: True for RTX GPUs (Turing+)
    /// </summary>
    public bool UseFp16 { get; set; } = true;

    /// <summary>
    /// Number of parallel GPU streams
    /// Higher values improve throughput on powerful GPUs
    /// Range: 1-4, Default: 1
    /// Recommended: 2 for RTX 3080+, 1 for RTX 3060/3070
    /// </summary>
    public int NumStreams { get; set; } = 1;

    // === Backend Configuration ===

    /// <summary>
    /// Processing backend to use
    /// 0 = TensorRT (fastest, NVIDIA only, RTX recommended)
    /// 1 = CUDA (compatible, NVIDIA only)
    /// 2 = CPU OpenVINO (slow, CPU fallback)
    /// </summary>
    public int Backend { get; set; } = 0; // TensorRT

    // === Helper Methods ===

    /// <summary>
    /// Get all available noise levels
    /// </summary>
    public static int[] GetAvailableNoiseLevels()
    {
        return [-1, 0, 1, 2, 3];
    }

    /// <summary>
    /// Get available scale factors
    /// </summary>
    public static int[] GetAvailableScaleFactors()
    {
        return [2, 3, 4];
    }

    /// <summary>
    /// Get available backends
    /// </summary>
    public static string[] GetAvailableBackends()
    {
        return ["TensorRT (NVIDIA, Fastest)", "CUDA (NVIDIA, Compatible)", "CPU (OpenVINO, Fallback)"];
    }

    /// <summary>
    /// Check if noise level is compatible with scale factor
    /// Noise levels 1 and 2 only work with scale=2
    /// </summary>
    public bool IsNoiseScaleCompatible()
    {
        if (Scale == 2)
            return true; // All noise levels work with 2x

        // For 3x and 4x, only noise -1, 0, and 3 are supported
        return Noise == -1 || Noise == 0 || Noise == 3;
    }

    /// <summary>
    /// Get display name for noise level
    /// </summary>
    public static string GetNoiseDisplayName(int noise)
    {
        return noise switch
        {
            -1 => "No Denoising",
            0 => "Conservative Denoising",
            1 => "Light Denoising (2x only)",
            2 => "Medium Denoising (2x only)",
            3 => "Aggressive Denoising",
            _ => $"Unknown ({noise})"
        };
    }

    /// <summary>
    /// Get display name for scale factor
    /// </summary>
    public static string GetScaleDisplayName(int scale)
    {
        return scale switch
        {
            2 => "2x (720p -> 1440p, 1080p -> 4K)",
            3 => "3x (720p -> 2160p)",
            4 => "4x (540p -> 2160p, 720p -> 2880p)",
            _ => $"{scale}x"
        };
    }

    /// <summary>
    /// Get display name for backend
    /// </summary>
    public static string GetBackendDisplayName(int backend)
    {
        return backend switch
        {
            0 => "TensorRT (NVIDIA, Fastest)",
            1 => "CUDA (NVIDIA, Compatible)",
            2 => "CPU OpenVINO (Fallback)",
            _ => $"Unknown ({backend})"
        };
    }

    /// <summary>
    /// Estimate processing speed in frames per second
    /// Real-CUGAN is 10-13x faster than Real-ESRGAN
    /// </summary>
    public double EstimateProcessingSpeedFPS(int inputWidth, int inputHeight, string gpuModel)
    {
        // Base speed estimates for RTX 3080 at 1080p -> 4K with TensorRT backend
        double baseSpeedRTX3080 = Backend == 0 ? 15.0 : 10.0; // TensorRT vs CUDA

        // Adjust for resolution (processing time scales with pixel count)
        double resolutionFactor = (1920.0 * 1080.0) / (inputWidth * inputHeight);

        // Adjust for scale factor (higher scales are slower)
        double scaleFactor = Scale switch
        {
            2 => 1.0,
            3 => 0.7,
            4 => 0.5,
            _ => 1.0
        };

        // Adjust for FP16 (faster)
        double fp16Factor = UseFp16 ? 1.5 : 1.0;

        // Adjust for num_streams (parallel processing)
        double streamsFactor = 1.0 + (NumStreams - 1) * 0.3; // Diminishing returns

        // GPU-specific adjustments (rough estimates)
        double gpuFactor = gpuModel.ToLower() switch
        {
            var g when g.Contains("4090") => 2.5,
            var g when g.Contains("4080") => 2.0,
            var g when g.Contains("4070") => 1.5,
            var g when g.Contains("3090") => 1.3,
            var g when g.Contains("3080") => 1.0,
            var g when g.Contains("3070") => 0.8,
            var g when g.Contains("3060") => 0.6,
            var g when g.Contains("2080") => 0.7,
            var g when g.Contains("2070") => 0.5,
            var g when g.Contains("2060") => 0.4,
            _ => 0.5 // Unknown GPU
        };

        // CPU backend is much slower
        if (Backend == 2)
            return baseSpeedRTX3080 * 0.1 * resolutionFactor * scaleFactor;

        return baseSpeedRTX3080 * resolutionFactor * scaleFactor * fp16Factor * streamsFactor * gpuFactor;
    }

    /// <summary>
    /// Get recommended settings for a given resolution
    /// </summary>
    public static RealCuganOptions GetRecommendedSettings(int inputHeight, bool aggressiveDenoise = false)
    {
        return new RealCuganOptions
        {
            Noise = aggressiveDenoise ? 3 : -1,
            Scale = inputHeight <= 720 ? 2 : 2, // Conservative 2x for safety
            UseFp16 = true, // Always use FP16 for RTX GPUs
            NumStreams = 2, // Good balance for most GPUs
            Backend = 0, // TensorRT (fastest)
            GpuId = 0
        };
    }

    /// <summary>
    /// Compare performance with Real-ESRGAN
    /// Returns speed multiplier (e.g., 12.0 means 12x faster)
    /// </summary>
    public double GetSpeedMultiplierVsEsrgan()
    {
        // Real-CUGAN with TensorRT is typically 10-13x faster than Real-ESRGAN
        // Real-ESRGAN: ~0.5-1 fps on RTX 3080
        // Real-CUGAN: ~10-20 fps on RTX 3080

        return Backend switch
        {
            0 => UseFp16 ? 13.0 : 10.0, // TensorRT with/without FP16
            1 => UseFp16 ? 10.0 : 8.0,  // CUDA with/without FP16
            2 => 2.0,                    // CPU (still faster than ESRGAN but much slower)
            _ => 10.0
        };
    }
}
