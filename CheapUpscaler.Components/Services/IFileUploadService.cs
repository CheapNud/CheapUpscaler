namespace CheapUpscaler.Components.Services;

/// <summary>
/// Platform abstraction for uploading files to the server.
/// Web implementations stream files to the server; desktop implementations return SupportsUpload=false.
/// </summary>
public interface IFileUploadService
{
    /// <summary>
    /// Indicates whether this platform supports file uploads.
    /// When false, upload UI should be hidden (desktop uses native file dialogs instead).
    /// </summary>
    bool SupportsUpload { get; }

    /// <summary>
    /// Maximum file size allowed for upload in bytes.
    /// </summary>
    long MaxFileSizeBytes { get; }

    /// <summary>
    /// Allowed file extensions (without dots), e.g., ["mp4", "mkv"].
    /// </summary>
    string[] AllowedExtensions { get; }

    /// <summary>
    /// Server directory where uploaded files are saved.
    /// </summary>
    string UploadDirectory { get; }

    /// <summary>
    /// Uploads a file to the server.
    /// </summary>
    /// <param name="fileStream">Stream containing the file data.</param>
    /// <param name="fileName">Original filename.</param>
    /// <param name="fileSize">Total file size in bytes.</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Upload result with server path or error message.</returns>
    Task<FileUploadResult> UploadFileAsync(
        Stream fileStream,
        string fileName,
        long fileSize,
        IProgress<FileUploadProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a list of recently uploaded files.
    /// </summary>
    Task<IReadOnlyList<UploadedFileInfo>> GetRecentUploadsAsync();

    /// <summary>
    /// Deletes an uploaded file.
    /// </summary>
    /// <param name="filePath">Full path to the file.</param>
    /// <returns>True if deleted successfully.</returns>
    Task<bool> DeleteUploadedFileAsync(string filePath);
}

/// <summary>
/// Result of a file upload operation.
/// </summary>
public record FileUploadResult
{
    /// <summary>
    /// Whether the upload completed successfully.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Full server path to the uploaded file (only set on success).
    /// </summary>
    public string? ServerPath { get; init; }

    /// <summary>
    /// Error message (only set on failure).
    /// </summary>
    public string? ErrorMessage { get; init; }

    public static FileUploadResult Ok(string serverPath) => new() { Success = true, ServerPath = serverPath };
    public static FileUploadResult Fail(string errorMessage) => new() { Success = false, ErrorMessage = errorMessage };
}

/// <summary>
/// Progress information during file upload.
/// </summary>
public record FileUploadProgress
{
    /// <summary>
    /// Number of bytes uploaded so far.
    /// </summary>
    public long BytesUploaded { get; init; }

    /// <summary>
    /// Total file size in bytes.
    /// </summary>
    public long TotalBytes { get; init; }

    /// <summary>
    /// Upload speed in bytes per second.
    /// </summary>
    public double BytesPerSecond { get; init; }

    /// <summary>
    /// Percentage of upload complete (0-100).
    /// </summary>
    public double PercentComplete => TotalBytes > 0 ? (double)BytesUploaded / TotalBytes * 100 : 0;

    /// <summary>
    /// Estimated time remaining.
    /// </summary>
    public TimeSpan? EstimatedTimeRemaining =>
        BytesPerSecond > 0 ? TimeSpan.FromSeconds((TotalBytes - BytesUploaded) / BytesPerSecond) : null;
}

/// <summary>
/// Information about an uploaded file.
/// </summary>
public record UploadedFileInfo
{
    /// <summary>
    /// File name without path.
    /// </summary>
    public required string FileName { get; init; }

    /// <summary>
    /// Full server path to the file.
    /// </summary>
    public required string FullPath { get; init; }

    /// <summary>
    /// File size in bytes.
    /// </summary>
    public required long Size { get; init; }

    /// <summary>
    /// When the file was uploaded.
    /// </summary>
    public required DateTime UploadedAt { get; init; }
}
