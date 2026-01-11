using CheapUpscaler.Blazor.Models;

namespace CheapUpscaler.Blazor.Services;

/// <summary>
/// Service for extracting video file metadata
/// </summary>
public interface IVideoInfoService
{
    /// <summary>
    /// Get video information for a file
    /// </summary>
    /// <param name="filePath">Path to the video file</param>
    /// <returns>Video metadata or null if extraction failed</returns>
    Task<VideoInfo?> GetVideoInfoAsync(string filePath);

    /// <summary>
    /// Generate a thumbnail image from the video
    /// </summary>
    /// <param name="filePath">Path to the video file</param>
    /// <param name="outputPath">Path to save the thumbnail</param>
    /// <param name="timeOffset">Time offset into the video (default: 10% of duration)</param>
    /// <returns>True if thumbnail was generated successfully</returns>
    Task<bool> GenerateThumbnailAsync(string filePath, string outputPath, TimeSpan? timeOffset = null);
}
