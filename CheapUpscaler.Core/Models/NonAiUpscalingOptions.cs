namespace CheapUpscaler.Core.Models;

/// <summary>
/// Configuration options for non-AI upscaling (xBR, Lanczos, HQx)
/// Ultra-fast alternatives to Real-ESRGAN (seconds vs hours)
/// </summary>
public class NonAiUpscalingOptions
{
    /// <summary>
    /// Upscaling algorithm to use
    /// Options: "xbr", "lanczos", "hqx"
    /// </summary>
    public string Algorithm { get; set; } = "lanczos";

    /// <summary>
    /// Upscaling factor (2, 3, or 4)
    /// 2x: 720p -> 1440p, 1080p -> 2160p
    /// 3x: 720p -> 2160p
    /// 4x: 540p -> 2160p, 720p -> 2880p
    /// </summary>
    public int ScaleFactor { get; set; } = 2;

    /// <summary>
    /// Get recommended algorithm based on content type
    /// </summary>
    public static string GetRecommendedAlgorithm(string contentType)
    {
        return contentType.ToLower() switch
        {
            "anime" => "xbr",        // Pattern-recognition works great for anime
            "cartoon" => "xbr",      // Also good for cartoons
            "pixel-art" => "hqx",    // HQx designed for pixel art
            "retro" => "hqx",        // Also good for retro content
            "general" => "lanczos",  // Lanczos is smooth and fast
            "photo" => "lanczos",    // Lanczos best for smooth gradients
            "live-action" => "lanczos", // Lanczos for real-world content
            _ => "lanczos"
        };
    }

    /// <summary>
    /// Get all available algorithms
    /// </summary>
    public static string[] GetAvailableAlgorithms()
    {
        return ["lanczos", "xbr", "hqx"];
    }

    /// <summary>
    /// Get available scale factors
    /// </summary>
    public static int[] GetAvailableScaleFactors()
    {
        return [2, 3, 4];
    }

    /// <summary>
    /// Get algorithm display name with description
    /// </summary>
    public static string GetAlgorithmDisplayName(string algorithm)
    {
        return algorithm.ToLower() switch
        {
            "lanczos" => "Lanczos (Smooth, General Content)",
            "xbr" => "xBR (Sharp, Anime/Pixel Art)",
            "hqx" => "HQx (High-Quality, Pixel Art)",
            _ => algorithm
        };
    }

    /// <summary>
    /// Get algorithm description
    /// </summary>
    public static string GetAlgorithmDescription(string algorithm)
    {
        return algorithm.ToLower() switch
        {
            "lanczos" => "Traditional resampling algorithm. Best for smooth gradients, photos, and general content. Extremely fast.",
            "xbr" => "Pattern-recognition algorithm. Preserves sharp edges and details. Great for anime, cartoons, and pixel art.",
            "hqx" => "High-quality magnification algorithm. Specifically designed for pixel art, sprites, and retro game footage.",
            _ => "Unknown algorithm"
        };
    }

    /// <summary>
    /// Estimate processing speed multiplier (relative to real-time)
    /// </summary>
    public static double GetSpeedMultiplier(string algorithm)
    {
        return algorithm.ToLower() switch
        {
            "lanczos" => 10.0,  // ~10x real-time (extremely fast)
            "xbr" => 3.0,       // ~3x real-time (fast)
            "hqx" => 5.0,       // ~5x real-time (very fast)
            _ => 5.0
        };
    }

    /// <summary>
    /// Estimate processing time for a given video duration
    /// </summary>
    public static TimeSpan EstimateProcessingTime(string algorithm, TimeSpan videoDuration)
    {
        var speedMultiplier = GetSpeedMultiplier(algorithm);
        return TimeSpan.FromSeconds(videoDuration.TotalSeconds / speedMultiplier);
    }

    /// <summary>
    /// Get recommended settings for a given content type and resolution
    /// </summary>
    public static NonAiUpscalingOptions GetRecommendedSettings(string contentType, int inputHeight)
    {
        return new NonAiUpscalingOptions
        {
            Algorithm = GetRecommendedAlgorithm(contentType),
            ScaleFactor = inputHeight <= 720 ? 2 : 2 // Conservative: always 2x for safety
        };
    }

    /// <summary>
    /// Validate options
    /// </summary>
    public bool IsValid(out string errorMessage)
    {
        // Validate algorithm
        if (!GetAvailableAlgorithms().Contains(Algorithm.ToLower()))
        {
            errorMessage = $"Invalid algorithm: {Algorithm}. Must be one of: {string.Join(", ", GetAvailableAlgorithms())}";
            return false;
        }

        // Validate scale factor
        if (!GetAvailableScaleFactors().Contains(ScaleFactor))
        {
            errorMessage = $"Invalid scale factor: {ScaleFactor}. Must be one of: {string.Join(", ", GetAvailableScaleFactors())}";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    /// <summary>
    /// Get FFmpeg filter string for this configuration
    /// </summary>
    public string GetFfmpegFilterString()
    {
        return Algorithm.ToLower() switch
        {
            "xbr" => $"xbr={ScaleFactor}",
            "hqx" => $"hqx={ScaleFactor}",
            "lanczos" => $"scale=iw*{ScaleFactor}:ih*{ScaleFactor}:flags=lanczos",
            _ => $"scale=iw*{ScaleFactor}:ih*{ScaleFactor}:flags=lanczos"
        };
    }

    /// <summary>
    /// Get output resolution for given input resolution
    /// </summary>
    public (int width, int height) GetOutputResolution(int inputWidth, int inputHeight)
    {
        return (inputWidth * ScaleFactor, inputHeight * ScaleFactor);
    }

    /// <summary>
    /// Format output resolution as string (e.g., "2880p")
    /// </summary>
    public string GetOutputResolutionString(int inputHeight)
    {
        return $"{inputHeight * ScaleFactor}p";
    }
}
