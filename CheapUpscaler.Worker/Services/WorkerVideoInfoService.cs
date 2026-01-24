using CheapUpscaler.Components.Models;
using CheapUpscaler.Components.Services;
using FFMpegCore;
using Microsoft.Extensions.Logging;

namespace CheapUpscaler.Worker.Services;

/// <summary>
/// Extracts video metadata using FFprobe via FFMpegCore.
/// Optimized for Docker/headless environments where FFmpeg is in PATH.
/// </summary>
public class WorkerVideoInfoService(ILogger<WorkerVideoInfoService> logger) : IVideoInfoService
{
    private bool _isConfigured;

    public async Task<VideoInfo?> GetVideoInfoAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            logger.LogWarning("Video file not found: {FilePath}", filePath);
            return null;
        }

        try
        {
            ConfigureFFmpeg();

            var mediaInfo = await FFProbe.AnalyseAsync(filePath);
            if (mediaInfo == null)
            {
                logger.LogWarning("FFProbe returned null for: {FilePath}", filePath);
                return null;
            }

            var videoStream = mediaInfo.PrimaryVideoStream;
            if (videoStream == null)
            {
                logger.LogWarning("No video stream found in: {FilePath}", filePath);
                return null;
            }

            var audioStream = mediaInfo.PrimaryAudioStream;
            var fileInfo = new FileInfo(filePath);

            return new VideoInfo
            {
                FilePath = filePath,
                FileName = fileInfo.Name,
                FileSizeBytes = fileInfo.Length,
                Duration = mediaInfo.Duration,
                Width = videoStream.Width,
                Height = videoStream.Height,
                FrameRate = videoStream.FrameRate,
                VideoCodec = videoStream.CodecName ?? "unknown",
                AudioCodec = audioStream?.CodecName,
                Format = mediaInfo.Format.FormatName ?? Path.GetExtension(filePath).TrimStart('.'),
                VideoBitrateKbps = videoStream.BitRate > 0 ? videoStream.BitRate / 1000.0 : null,
                AudioBitrateKbps = audioStream?.BitRate > 0 ? audioStream.BitRate / 1000.0 : null,
                PixelFormat = videoStream.PixelFormat
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error extracting video info from {FilePath}", filePath);
            return null;
        }
    }

    public async Task<bool> GenerateThumbnailAsync(string filePath, string outputPath, TimeSpan? timeOffset = null)
    {
        if (!File.Exists(filePath))
        {
            logger.LogWarning("Video file not found: {FilePath}", filePath);
            return false;
        }

        try
        {
            ConfigureFFmpeg();

            var mediaInfo = await FFProbe.AnalyseAsync(filePath);
            if (mediaInfo == null) return false;

            // Default to 10% into the video, or 1 second minimum
            var captureTime = timeOffset ?? TimeSpan.FromSeconds(
                Math.Max(1, mediaInfo.Duration.TotalSeconds * 0.1));

            // Ensure we don't exceed video duration
            if (captureTime > mediaInfo.Duration)
            {
                captureTime = TimeSpan.FromSeconds(mediaInfo.Duration.TotalSeconds / 2);
            }

            // Ensure output directory exists
            var outputDir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            await FFMpeg.SnapshotAsync(filePath, outputPath, captureTime: captureTime);
            return File.Exists(outputPath);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error generating thumbnail for {FilePath}", filePath);
            return false;
        }
    }

    private void ConfigureFFmpeg()
    {
        if (_isConfigured) return;

        // In Docker, ffmpeg is typically in PATH (/usr/bin/ffmpeg)
        // Try common locations first
        var ffmpegPath = FindFFmpegPath();
        if (!string.IsNullOrEmpty(ffmpegPath))
        {
            var directory = Path.GetDirectoryName(ffmpegPath);
            if (!string.IsNullOrEmpty(directory))
            {
                GlobalFFOptions.Configure(options =>
                {
                    options.BinaryFolder = directory;
                });
                logger.LogInformation("FFMpegCore configured with path: {Directory}", directory);
            }
        }
        else
        {
            // Assume ffmpeg is in PATH (typical for Docker)
            logger.LogInformation("FFmpeg not found in custom paths, assuming it's in PATH");
        }

        _isConfigured = true;
    }

    private static string? FindFFmpegPath()
    {
        // Check common Linux locations (for Docker)
        if (!OperatingSystem.IsWindows())
        {
            var linuxPaths = new[]
            {
                "/usr/bin/ffmpeg",
                "/usr/local/bin/ffmpeg"
            };

            foreach (var path in linuxPaths)
            {
                if (File.Exists(path))
                {
                    return path;
                }
            }
        }

        // Check common Windows locations
        if (OperatingSystem.IsWindows())
        {
            var windowsPaths = new[]
            {
                @"C:\ffmpeg\bin\ffmpeg.exe",
                @"C:\Program Files\ffmpeg\bin\ffmpeg.exe",
                @"C:\Program Files (x86)\ffmpeg\bin\ffmpeg.exe",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ffmpeg", "bin", "ffmpeg.exe")
            };

            foreach (var path in windowsPaths)
            {
                if (File.Exists(path))
                {
                    return path;
                }
            }
        }

        // Fallback: assume it's in PATH (FFMpegCore will handle this)
        return null;
    }
}
