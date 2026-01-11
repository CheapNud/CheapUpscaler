using System.Text.Json;

namespace CheapUpscaler.Blazor.Models;

/// <summary>
/// Represents a video upscaling/interpolation job in the queue
/// </summary>
public class UpscaleJob
{
    /// <summary>Database primary key</summary>
    public int Id { get; set; }

    /// <summary>Unique job identifier</summary>
    public Guid JobId { get; set; } = Guid.NewGuid();

    /// <summary>Path to source video file</summary>
    public string SourceVideoPath { get; set; } = string.Empty;

    /// <summary>Path where output will be saved</summary>
    public string OutputPath { get; set; } = string.Empty;

    /// <summary>Type of upscaling to apply</summary>
    public UpscaleType UpscaleType { get; set; }

    /// <summary>JSON-serialized settings specific to the upscale type</summary>
    public string SettingsJson { get; set; } = "{}";

    /// <summary>Current job status</summary>
    public UpscaleJobStatus Status { get; set; } = UpscaleJobStatus.Pending;

    // Timestamps
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? QueuedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;

    // Progress tracking
    public double ProgressPercentage { get; set; }
    public int CurrentFrame { get; set; }
    public int? TotalFrames { get; set; }
    public TimeSpan? EstimatedTimeRemaining { get; set; }

    // Source video info
    public int? SourceWidth { get; set; }
    public int? SourceHeight { get; set; }
    public double? SourceFps { get; set; }
    public TimeSpan? SourceDuration { get; set; }

    // Output info
    public int? OutputWidth { get; set; }
    public int? OutputHeight { get; set; }
    public double? OutputFps { get; set; }
    public long? OutputFileSizeBytes { get; set; }

    // Error handling
    public int RetryCount { get; set; }
    public int MaxRetries { get; set; } = 3;
    public string? LastError { get; set; }
    public string? ErrorStackTrace { get; set; }

    // Process tracking for crash recovery
    public int? ProcessId { get; set; }
    public string? MachineName { get; set; }

    /// <summary>
    /// Get formatted output file size
    /// </summary>
    public string GetOutputFileSizeFormatted()
    {
        if (!OutputFileSizeBytes.HasValue) return "N/A";
        return FormatBytes(OutputFileSizeBytes.Value);
    }

    /// <summary>
    /// Convert frame number to timecode string (HH:MM:SS.mmm)
    /// </summary>
    public string FramesToTimecode(int frames)
    {
        var fps = SourceFps ?? 30.0;
        var totalSeconds = frames / fps;
        var ts = TimeSpan.FromSeconds(totalSeconds);
        return $"{ts.Hours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}.{ts.Milliseconds:D3}";
    }

    /// <summary>
    /// Get display name for the upscale type
    /// </summary>
    public string GetUpscaleTypeDisplayName() => UpscaleType switch
    {
        UpscaleType.Rife => "RIFE Interpolation",
        UpscaleType.RealCugan => "Real-CUGAN",
        UpscaleType.RealEsrgan => "Real-ESRGAN",
        UpscaleType.NonAi => "Non-AI Upscaling",
        _ => "Unknown"
    };

    /// <summary>
    /// Get settings object deserialized from JSON
    /// </summary>
    public T? GetSettings<T>() where T : class
    {
        if (string.IsNullOrEmpty(SettingsJson)) return null;
        try
        {
            return JsonSerializer.Deserialize<T>(SettingsJson);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Set settings from object (serializes to JSON)
    /// </summary>
    public void SetSettings<T>(T settings) where T : class
    {
        SettingsJson = JsonSerializer.Serialize(settings);
    }

    private static string FormatBytes(long bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB", "TB"];
        double len = bytes;
        int order = 0;

        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }

        return $"{len:F2} {sizes[order]}";
    }
}
