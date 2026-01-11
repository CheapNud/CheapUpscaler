using System.Diagnostics;
using CheapHelpers.MediaProcessing.Services;
using CheapUpscaler.Blazor.Models;
using FFMpegCore;

namespace CheapUpscaler.Blazor.Services;

/// <summary>
/// Extracts video metadata using FFprobe via FFMpegCore
/// </summary>
public class VideoInfoService(ExecutableDetectionService executableDetection) : IVideoInfoService
{
    private bool _isConfigured;

    public async Task<VideoInfo?> GetVideoInfoAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            Debug.WriteLine($"Video file not found: {filePath}");
            return null;
        }

        try
        {
            ConfigureFFmpeg();

            var mediaInfo = await FFProbe.AnalyseAsync(filePath);
            if (mediaInfo == null)
            {
                Debug.WriteLine($"FFProbe returned null for: {filePath}");
                return null;
            }

            var videoStream = mediaInfo.PrimaryVideoStream;
            if (videoStream == null)
            {
                Debug.WriteLine($"No video stream found in: {filePath}");
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
            Debug.WriteLine($"Error extracting video info: {ex.Message}");
            return null;
        }
    }

    public async Task<bool> GenerateThumbnailAsync(string filePath, string outputPath, TimeSpan? timeOffset = null)
    {
        if (!File.Exists(filePath))
        {
            Debug.WriteLine($"Video file not found: {filePath}");
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
            Debug.WriteLine($"Error generating thumbnail: {ex.Message}");
            return false;
        }
    }

    private void ConfigureFFmpeg()
    {
        if (_isConfigured) return;

        // Try to find FFmpeg/FFprobe from various sources
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
                Debug.WriteLine($"FFMpegCore configured with path: {directory}");
            }
        }

        _isConfigured = true;
    }

    private string? FindFFmpegPath()
    {
        // Check if ExecutableDetectionService can find it
        var ffmpegPath = executableDetection.DetectFFmpeg(useSvpEncoders: false, customPath: null);
        if (!string.IsNullOrEmpty(ffmpegPath))
        {
            return ffmpegPath;
        }

        // Check common Windows locations
        var commonPaths = new[]
        {
            @"C:\ffmpeg\bin\ffmpeg.exe",
            @"C:\Program Files\ffmpeg\bin\ffmpeg.exe",
            @"C:\Program Files (x86)\ffmpeg\bin\ffmpeg.exe",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ffmpeg", "bin", "ffmpeg.exe")
        };

        foreach (var path in commonPaths)
        {
            if (File.Exists(path))
            {
                return path;
            }
        }

        // Fallback: assume it's in PATH (FFMpegCore will handle this)
        return null;
    }
}
