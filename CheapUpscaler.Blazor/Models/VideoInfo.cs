namespace CheapUpscaler.Blazor.Models;

/// <summary>
/// Video file metadata for display in the UI
/// </summary>
public class VideoInfo
{
    /// <summary>File path</summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>File name without path</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>File size in bytes</summary>
    public long FileSizeBytes { get; set; }

    /// <summary>Video duration</summary>
    public TimeSpan Duration { get; set; }

    /// <summary>Video width in pixels</summary>
    public int Width { get; set; }

    /// <summary>Video height in pixels</summary>
    public int Height { get; set; }

    /// <summary>Frame rate (frames per second)</summary>
    public double FrameRate { get; set; }

    /// <summary>Total frame count (estimated)</summary>
    public int TotalFrames => (int)(Duration.TotalSeconds * FrameRate);

    /// <summary>Video codec (e.g., h264, hevc, vp9)</summary>
    public string VideoCodec { get; set; } = string.Empty;

    /// <summary>Audio codec (e.g., aac, mp3, opus)</summary>
    public string? AudioCodec { get; set; }

    /// <summary>Container format (e.g., mp4, mkv, avi)</summary>
    public string Format { get; set; } = string.Empty;

    /// <summary>Video bitrate in kbps</summary>
    public double? VideoBitrateKbps { get; set; }

    /// <summary>Audio bitrate in kbps</summary>
    public double? AudioBitrateKbps { get; set; }

    /// <summary>Pixel format (e.g., yuv420p, yuv444p)</summary>
    public string? PixelFormat { get; set; }

    /// <summary>Has audio stream</summary>
    public bool HasAudio => !string.IsNullOrEmpty(AudioCodec);

    /// <summary>Resolution display string (e.g., "1920x1080")</summary>
    public string Resolution => $"{Width}x{Height}";

    /// <summary>Resolution category (e.g., "1080p", "4K")</summary>
    public string ResolutionLabel => Height switch
    {
        >= 2160 => "4K UHD",
        >= 1440 => "1440p QHD",
        >= 1080 => "1080p FHD",
        >= 720 => "720p HD",
        >= 576 => "576p SD",
        >= 480 => "480p",
        _ => $"{Height}p"
    };

    /// <summary>Formatted duration (HH:MM:SS)</summary>
    public string DurationFormatted => Duration.TotalHours >= 1
        ? $"{(int)Duration.TotalHours}:{Duration.Minutes:D2}:{Duration.Seconds:D2}"
        : $"{Duration.Minutes}:{Duration.Seconds:D2}";

    /// <summary>Formatted file size (e.g., "1.5 GB")</summary>
    public string FileSizeFormatted => FormatBytes(FileSizeBytes);

    /// <summary>Formatted frame rate (e.g., "23.976 fps")</summary>
    public string FrameRateFormatted => FrameRate % 1 == 0
        ? $"{(int)FrameRate} fps"
        : $"{FrameRate:F3} fps";

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
