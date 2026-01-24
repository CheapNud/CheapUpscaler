using CheapUpscaler.Components.Services;

namespace CheapUpscaler.Blazor.Services;

/// <summary>
/// Desktop stub for file upload service.
/// Desktop applications use native file dialogs instead of uploads.
/// </summary>
public class DesktopUploadService : IFileUploadService
{
    public bool SupportsUpload => false;

    public long MaxFileSizeBytes => 0;

    public string[] AllowedExtensions => [];

    public string UploadDirectory => string.Empty;

    public Task<FileUploadResult> UploadFileAsync(
        Stream fileStream,
        string fileName,
        long fileSize,
        IProgress<FileUploadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(FileUploadResult.Fail("Upload not supported on desktop. Use native file dialogs."));
    }

    public Task<IReadOnlyList<UploadedFileInfo>> GetRecentUploadsAsync()
    {
        return Task.FromResult<IReadOnlyList<UploadedFileInfo>>([]);
    }

    public Task<bool> DeleteUploadedFileAsync(string filePath)
    {
        return Task.FromResult(false);
    }
}
