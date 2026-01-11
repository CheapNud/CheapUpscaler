using CheapHelpers.MediaProcessing.Models;

namespace CheapUpscaler.Core.Services.RIFE;

/// <summary>
/// Options for the complete RIFE video processing pipeline
/// </summary>
public class RifePipelineOptions
{
    /// <summary>
    /// RIFE interpolation options
    /// </summary>
    public RifeOptions RifeOptions { get; set; } = new();

    /// <summary>
    /// FFmpeg rendering settings
    /// If null, auto-detect based on hardware (RTX 3080)
    /// </summary>
    public FFmpegRenderSettings? FFmpegSettings { get; set; }

    /// <summary>
    /// Enable hardware decoding for frame extraction
    /// Recommended: true if you have NVIDIA GPU
    /// </summary>
    public bool UseHardwareDecode { get; set; } = true;

    /// <summary>
    /// Keep temporary files after processing
    /// Useful for debugging
    /// </summary>
    public bool KeepTemporaryFiles { get; set; } = false;

    /// <summary>
    /// Validate frame counts at each step
    /// Recommended: true to catch errors early
    /// </summary>
    public bool ValidateFrameCounts { get; set; } = true;

    /// <summary>
    /// Target FPS for input frame extraction
    /// If null, use source video FPS
    /// </summary>
    public int? InputFps { get; set; }

    /// <summary>
    /// Calculate output FPS based on interpolation multiplier
    /// </summary>
    public int CalculateOutputFps(int inputFps)
    {
        return inputFps * RifeOptions.GetFrameMultiplier();
    }

    /// <summary>
    /// Create default options optimized for RTX 3080
    /// </summary>
    public static RifePipelineOptions CreateDefault()
    {
        return new RifePipelineOptions
        {
            RifeOptions = new RifeOptions
            {
                ModelName = "4.6",
                GpuId = 0,
                InterpolationPasses = 1 // 2x interpolation (30fps -> 60fps)
            },
            UseHardwareDecode = true,
            ValidateFrameCounts = true,
            KeepTemporaryFiles = false
        };
    }

    /// <summary>
    /// Create high-quality options for best results
    /// </summary>
    public static RifePipelineOptions CreateHighQuality()
    {
        return new RifePipelineOptions
        {
            RifeOptions = new RifeOptions
            {
                ModelName = "4.25", // Latest stable model
                GpuId = 0,
                InterpolationPasses = 1,
                TtaMode = true // Better quality but slower
            },
            UseHardwareDecode = true,
            ValidateFrameCounts = true,
            KeepTemporaryFiles = false,
            FFmpegSettings = new FFmpegRenderSettings
            {
                UseHardwareAcceleration = true,
                VideoCodec = "hevc_nvenc",
                NvencPreset = "p7",
                Quality = 18 // Higher quality
            }
        };
    }

    /// <summary>
    /// Create fast options for quick processing
    /// </summary>
    public static RifePipelineOptions CreateFast()
    {
        return new RifePipelineOptions
        {
            RifeOptions = new RifeOptions
            {
                ModelName = "4.16-lite", // Fastest lite model
                GpuId = 0,
                InterpolationPasses = 1
            },
            UseHardwareDecode = true,
            ValidateFrameCounts = true,
            KeepTemporaryFiles = false,
            FFmpegSettings = new FFmpegRenderSettings
            {
                UseHardwareAcceleration = true,
                VideoCodec = "hevc_nvenc",
                NvencPreset = "p4", // Faster preset
                Quality = 23
            }
        };
    }
}
