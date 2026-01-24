using System.Diagnostics;
using System.Text.Json;
using CheapUpscaler.Components.Services;

namespace CheapUpscaler.Worker.Services;

/// <summary>
/// Server-side file upload service for Docker/web environments.
/// Streams uploaded files to the configured input directory.
/// </summary>
public class ServerFileUploadService(IConfiguration configuration, ILogger<ServerFileUploadService> logger) : IFileUploadService
{
    private const int BufferSize = 81920; // 80KB buffer for optimal streaming
    private const int ProgressReportIntervalBytes = 1024 * 1024; // Report progress every 1MB

    private readonly string _inputPath = configuration["Worker:InputPath"] ?? "/data/input";
    private readonly string _uploadsMetadataFile = Path.Combine(
        configuration["Worker:DataPath"] ?? "/data",
        "uploads.json");

    public bool SupportsUpload => true;

    public long MaxFileSizeBytes => 10L * 1024 * 1024 * 1024; // 10 GB

    public string[] AllowedExtensions => ["mp4", "mkv", "avi", "mov", "webm", "wmv", "flv"];

    public string UploadDirectory => _inputPath;

    public async Task<FileUploadResult> UploadFileAsync(
        Stream fileStream,
        string fileName,
        long fileSize,
        IProgress<FileUploadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Validate extension
            var extension = Path.GetExtension(fileName).TrimStart('.').ToLowerInvariant();
            if (!AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            {
                return FileUploadResult.Fail($"File type '.{extension}' is not allowed.");
            }

            // Validate size
            if (fileSize > MaxFileSizeBytes)
            {
                return FileUploadResult.Fail($"File size exceeds maximum allowed ({MaxFileSizeBytes / (1024 * 1024 * 1024)} GB).");
            }

            // Ensure upload directory exists
            Directory.CreateDirectory(_inputPath);

            // Sanitize filename and generate unique name if collision
            var safeFileName = SanitizeFileName(fileName);
            var targetPath = GetUniqueFilePath(Path.Combine(_inputPath, safeFileName));

            logger.LogInformation("Starting upload: {FileName} ({Size} bytes) -> {TargetPath}",
                fileName, fileSize, targetPath);

            var stopwatch = Stopwatch.StartNew();
            long bytesWritten = 0;
            long lastProgressReport = 0;

            await using var outputStream = new FileStream(
                targetPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                BufferSize,
                FileOptions.Asynchronous | FileOptions.SequentialScan);

            var buffer = new byte[BufferSize];
            int bytesRead;

            while ((bytesRead = await fileStream.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await outputStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                bytesWritten += bytesRead;

                // Report progress at intervals
                if (progress != null && bytesWritten - lastProgressReport >= ProgressReportIntervalBytes)
                {
                    var elapsed = stopwatch.Elapsed.TotalSeconds;
                    var bytesPerSecond = elapsed > 0 ? bytesWritten / elapsed : 0;

                    progress.Report(new FileUploadProgress
                    {
                        BytesUploaded = bytesWritten,
                        TotalBytes = fileSize,
                        BytesPerSecond = bytesPerSecond
                    });

                    lastProgressReport = bytesWritten;
                }
            }

            // Final progress report
            progress?.Report(new FileUploadProgress
            {
                BytesUploaded = bytesWritten,
                TotalBytes = fileSize,
                BytesPerSecond = stopwatch.Elapsed.TotalSeconds > 0
                    ? bytesWritten / stopwatch.Elapsed.TotalSeconds
                    : 0
            });

            logger.LogInformation("Upload completed: {FileName} ({Size} bytes) in {Duration}ms",
                fileName, bytesWritten, stopwatch.ElapsedMilliseconds);

            // Track upload in metadata
            await TrackUploadAsync(new UploadedFileInfo
            {
                FileName = Path.GetFileName(targetPath),
                FullPath = targetPath,
                Size = bytesWritten,
                UploadedAt = DateTime.UtcNow
            });

            return FileUploadResult.Ok(targetPath);
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Upload cancelled: {FileName}", fileName);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Upload failed: {FileName}", fileName);
            return FileUploadResult.Fail($"Upload failed: {ex.Message}");
        }
    }

    public async Task<IReadOnlyList<UploadedFileInfo>> GetRecentUploadsAsync()
    {
        try
        {
            if (!File.Exists(_uploadsMetadataFile))
                return [];

            var json = await File.ReadAllTextAsync(_uploadsMetadataFile);
            var uploads = JsonSerializer.Deserialize<List<UploadedFileInfo>>(json) ?? [];

            // Filter out files that no longer exist and return most recent first
            var validUploads = uploads
                .Where(u => File.Exists(u.FullPath))
                .OrderByDescending(u => u.UploadedAt)
                .Take(20) // Keep only last 20
                .ToList();

            // Update file if we filtered some out
            if (validUploads.Count != uploads.Count)
            {
                await SaveUploadsMetadataAsync(validUploads);
            }

            return validUploads;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to read uploads metadata");
            return [];
        }
    }

    public async Task<bool> DeleteUploadedFileAsync(string filePath)
    {
        try
        {
            var normalizedPath = Path.GetFullPath(filePath);
            var normalizedInput = Path.GetFullPath(_inputPath);

            // Security: Only allow deleting files within input directory
            if (!normalizedPath.StartsWith(normalizedInput, StringComparison.OrdinalIgnoreCase))
            {
                logger.LogWarning("Attempted to delete file outside input directory: {FilePath}", filePath);
                return false;
            }

            if (!File.Exists(normalizedPath))
                return false;

            File.Delete(normalizedPath);

            // Remove from metadata
            var uploads = (await GetRecentUploadsAsync()).ToList();
            uploads.RemoveAll(u => u.FullPath.Equals(normalizedPath, StringComparison.OrdinalIgnoreCase));
            await SaveUploadsMetadataAsync(uploads);

            logger.LogInformation("Deleted uploaded file: {FilePath}", filePath);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete uploaded file: {FilePath}", filePath);
            return false;
        }
    }

    private async Task TrackUploadAsync(UploadedFileInfo uploadInfo)
    {
        try
        {
            var uploads = (await GetRecentUploadsAsync()).ToList();
            uploads.Insert(0, uploadInfo);

            // Keep only recent uploads
            if (uploads.Count > 50)
                uploads = uploads.Take(50).ToList();

            await SaveUploadsMetadataAsync(uploads);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to track upload metadata");
        }
    }

    private async Task SaveUploadsMetadataAsync(List<UploadedFileInfo> uploads)
    {
        var directory = Path.GetDirectoryName(_uploadsMetadataFile);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var json = JsonSerializer.Serialize(uploads, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_uploadsMetadataFile, json);
    }

    private static string SanitizeFileName(string fileName)
    {
        // Remove any path components
        fileName = Path.GetFileName(fileName);

        // Replace invalid characters with underscore
        var invalidChars = Path.GetInvalidFileNameChars();
        foreach (var c in invalidChars)
        {
            fileName = fileName.Replace(c, '_');
        }

        // Remove any remaining problematic characters
        fileName = fileName
            .Replace("..", "_")
            .Replace("~", "_")
            .Trim();

        return string.IsNullOrEmpty(fileName) ? "uploaded_file" : fileName;
    }

    private static string GetUniqueFilePath(string targetPath)
    {
        if (!File.Exists(targetPath))
            return targetPath;

        var directory = Path.GetDirectoryName(targetPath) ?? "";
        var fileNameWithoutExt = Path.GetFileNameWithoutExtension(targetPath);
        var extension = Path.GetExtension(targetPath);

        var counter = 1;
        string newPath;
        do
        {
            newPath = Path.Combine(directory, $"{fileNameWithoutExt}_{counter}{extension}");
            counter++;
        } while (File.Exists(newPath));

        return newPath;
    }
}
