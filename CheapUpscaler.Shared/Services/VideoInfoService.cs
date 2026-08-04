using CheapUpscaler.Shared.Models;
using FFMpegCore;
using Microsoft.Extensions.Logging;

namespace CheapUpscaler.Shared.Services;

/// <summary>
/// Extracts video metadata using FFprobe via FFMpegCore.
/// </summary>
/// <param name="logger">Host logger</param>
/// <param name="resolveFFmpegPath">
/// Optional host-specific ffmpeg resolution (the desktop host uses SVP/executable detection).
/// When it yields nothing, <see cref="ToolProbe.FindFFmpeg"/> checks the well-known install
/// locations, and failing that FFMpegCore falls back to whatever is on PATH.
/// </param>
public class VideoInfoService(ILogger<VideoInfoService> logger, Func<string?>? resolveFFmpegPath = null) : IVideoInfoService
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
        _isConfigured = true;

        var ffmpegPath = resolveFFmpegPath?.Invoke() ?? ToolProbe.FindFFmpeg();
        var directory = string.IsNullOrEmpty(ffmpegPath) ? null : Path.GetDirectoryName(ffmpegPath);

        if (!string.IsNullOrEmpty(directory))
        {
            GlobalFFOptions.Configure(options => options.BinaryFolder = directory);
            logger.LogInformation("FFMpegCore configured with path: {Directory}", directory);
        }
        else
        {
            logger.LogInformation("FFmpeg not found in known locations, assuming it is on PATH");
        }
    }
}
