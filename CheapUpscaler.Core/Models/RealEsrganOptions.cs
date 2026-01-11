namespace CheapUpscaler.Core.Models;

/// <summary>
/// Configuration options for Real-ESRGAN AI upscaling
/// Uses VapourSynth + TensorRT approach (matching RIFE integration)
/// </summary>
public class RealEsrganOptions
{
    // === Core Engine Settings ===

    /// <summary>
    /// AI model to use for upscaling
    /// Available models: RealESRGAN_x4plus, RealESRGAN_x4plus_anime_6B, RealESRGAN_x2plus,
    /// realesr-general-x4v3, RealESRGAN_AnimeVideo-v3
    /// </summary>
    public string ModelName { get; set; } = "RealESRGAN_x4plus";

    /// <summary>
    /// Upscaling factor (2x or 4x)
    /// 2x: 720p -> 1440p, 1080p -> 2160p
    /// 4x: 540p -> 2160p, 720p -> 2880p
    /// </summary>
    public int ScaleFactor { get; set; } = 4;

    /// <summary>
    /// GPU device ID to use for processing
    /// 0 = First GPU, 1 = Second GPU, etc.
    /// </summary>
    public int GpuId { get; set; } = 0;

    // === Performance Settings ===

    /// <summary>
    /// Enable tile mode for VRAM management
    /// True = Process video in tiles (slower but lower VRAM)
    /// False = Process full frames (faster but higher VRAM)
    /// </summary>
    public bool TileMode { get; set; } = true;

    /// <summary>
    /// Tile size in pixels (when TileMode is enabled)
    /// Smaller = less VRAM usage, slower processing
    /// Larger = more VRAM usage, faster processing
    /// Range: 128-1024, Default: 512
    /// </summary>
    public int TileSize { get; set; } = 512;

    /// <summary>
    /// Tile padding in pixels (overlap between tiles)
    /// Reduces artifacts at tile boundaries
    /// Range: 0-50, Default: 10
    /// </summary>
    public int TilePad { get; set; } = 10;

    /// <summary>
    /// Use FP16 (half precision) for faster processing
    /// True = 50% faster, slightly lower quality
    /// False = Slower, best quality
    /// Recommended: True for RTX GPUs
    /// </summary>
    public bool UseFp16 { get; set; } = true;

    /// <summary>
    /// Number of parallel processing threads
    /// Range: 1-4, Default: 1
    /// Higher values may improve speed on powerful GPUs
    /// </summary>
    public int NumThreads { get; set; } = 1;

    // === Quality Settings ===

    /// <summary>
    /// Enable denoising during upscaling
    /// Reduces compression artifacts and noise
    /// </summary>
    public bool EnableDenoising { get; set; } = false;

    /// <summary>
    /// Denoising strength (when enabled)
    /// Range: 0.0-1.0
    /// 0.5 = balanced, 0.8 = aggressive, 0.2 = subtle
    /// </summary>
    public float DenoisingStrength { get; set; } = 0.5f;

    /// <summary>
    /// Target output resolution height (optional)
    /// 0 = Auto (based on scale factor)
    /// Common values: 1440, 2160, 3840, 4320
    /// </summary>
    public int TargetHeight { get; set; } = 0;

    // === Helper Methods ===

    /// <summary>
    /// Get all available Real-ESRGAN models
    /// </summary>
    public static string[] GetAvailableModels()
    {
        return
        [
            "RealESRGAN_x4plus",              // General 4x upscaling (default)
            "RealESRGAN_x4plus_anime_6B",     // Anime-optimized 4x (6 billion params)
            "RealESRGAN_x2plus",              // 2x upscaling
            "realesr-general-x4v3",           // General 4x with denoising
            "RealESRGAN_AnimeVideo-v3"        // Video temporal consistency for anime
        ];
    }

    /// <summary>
    /// Get available scale factors
    /// </summary>
    public static int[] GetAvailableScaleFactors()
    {
        return [2, 4];
    }

    /// <summary>
    /// Get available tile sizes
    /// </summary>
    public static int[] GetAvailableTileSizes()
    {
        return [128, 256, 384, 512, 768, 1024];
    }

    /// <summary>
    /// Get model display name with description
    /// </summary>
    public static string GetModelDisplayName(string modelName)
    {
        return modelName switch
        {
            "RealESRGAN_x4plus" => "General 4x (Balanced)",
            "RealESRGAN_x4plus_anime_6B" => "Anime 4x (High Quality)",
            "RealESRGAN_x2plus" => "General 2x",
            "realesr-general-x4v3" => "General 4x + Denoising",
            "RealESRGAN_AnimeVideo-v3" => "Anime Video (Temporal)",
            _ => modelName
        };
    }

    /// <summary>
    /// Get recommended tile size for resolution and VRAM
    /// </summary>
    public static int GetRecommendedTileSize(int videoHeight, int vramGB)
    {
        // Calculate based on VRAM and input resolution
        if (vramGB >= 12)
        {
            // High VRAM (RTX 3080+, RTX 4080+)
            return videoHeight <= 720 ? 1024 : 768;
        }
        else if (vramGB >= 8)
        {
            // Medium VRAM (RTX 3070, RTX 3060 Ti)
            return videoHeight <= 720 ? 768 : 512;
        }
        else if (vramGB >= 6)
        {
            // Lower VRAM (RTX 3060, RTX 2060)
            return videoHeight <= 720 ? 512 : 384;
        }
        else
        {
            // Minimal VRAM (GTX 1660, etc.)
            return 256;
        }
    }

    /// <summary>
    /// Estimate VRAM usage in GB
    /// </summary>
    public double EstimateVramUsageGB(int inputWidth, int inputHeight)
    {
        // Base VRAM usage for model weights
        double baseVram = ModelName.Contains("anime_6B") ? 2.5 : 1.5;

        // Calculate processing VRAM based on tile size or full frame
        double processingVram;
        if (TileMode)
        {
            // Tile mode: VRAM depends on tile size
            processingVram = (TileSize * TileSize * ScaleFactor * ScaleFactor * 4.0) / (1024 * 1024 * 1024);
        }
        else
        {
            // Full frame mode: VRAM depends on full resolution
            processingVram = (inputWidth * inputHeight * ScaleFactor * ScaleFactor * 4.0) / (1024 * 1024 * 1024);
        }

        // Add overhead for TensorRT and intermediate buffers
        double overhead = 1.0;

        // FP16 uses half the VRAM
        double multiplier = UseFp16 ? 0.5 : 1.0;

        return (baseVram + processingVram + overhead) * multiplier;
    }

    /// <summary>
    /// Estimate processing speed in frames per second
    /// </summary>
    public double EstimateProcessingSpeedFPS(int inputWidth, int inputHeight, string gpuModel)
    {
        // Base speed estimates for RTX 3080 at 1080p -> 4K
        double baseSpeedRTX3080 = 3.0;

        // Adjust for resolution
        double resolutionFactor = (1920.0 * 1080.0) / (inputWidth * inputHeight);

        // Adjust for scale factor
        double scaleFactor = ScaleFactor == 2 ? 1.5 : 1.0;

        // Adjust for tile mode (slower but fits in VRAM)
        double tileModeFactor = TileMode ? 0.7 : 1.0;

        // Adjust for FP16 (faster)
        double fp16Factor = UseFp16 ? 1.5 : 1.0;

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

        return baseSpeedRTX3080 * resolutionFactor * scaleFactor * tileModeFactor * fp16Factor * gpuFactor;
    }

    /// <summary>
    /// Get recommended settings for a given resolution
    /// </summary>
    public static RealEsrganOptions GetRecommendedSettings(int inputHeight, bool isAnime = false)
    {
        return new RealEsrganOptions
        {
            ModelName = isAnime ? "RealESRGAN_x4plus_anime_6B" : "RealESRGAN_x4plus",
            ScaleFactor = inputHeight <= 720 ? 4 : 2,
            TileMode = true,
            TileSize = inputHeight <= 720 ? 512 : 384,
            TilePad = 10,
            UseFp16 = true,
            NumThreads = 1,
            EnableDenoising = false,
            DenoisingStrength = 0.5f,
            GpuId = 0
        };
    }
}
