using CheapUpscaler.Components.Services;
using Microsoft.Extensions.Configuration;

namespace CheapUpscaler.Worker.Services;

/// <summary>
/// Server-side file browser service for Docker/web environments.
/// Allows browsing files on the server filesystem.
/// </summary>
public class ServerFileBrowserService(IConfiguration configuration) : IFileBrowserService
{
    // Configurable allowed paths for security
    private readonly string _inputPath = configuration["Worker:InputPath"] ?? "/data/input";
    private readonly string _outputPath = configuration["Worker:OutputPath"] ?? "/data/output";

    public Task<FileBrowserResult> GetDirectoryContentsAsync(string path, string[]? fileExtensions = null)
    {
        try
        {
            // Security: Validate path is within allowed directories
            var normalizedPath = Path.GetFullPath(path);

            if (!IsPathAllowed(normalizedPath))
            {
                return Task.FromResult(new FileBrowserResult
                {
                    CurrentPath = path,
                    Items = [],
                    ErrorMessage = "Access denied. Path is outside allowed directories."
                });
            }

            if (!Directory.Exists(normalizedPath))
            {
                return Task.FromResult(new FileBrowserResult
                {
                    CurrentPath = path,
                    Items = [],
                    ErrorMessage = "Directory does not exist."
                });
            }

            var items = new List<FileBrowserItem>();

            // Add directories
            foreach (var dir in Directory.GetDirectories(normalizedPath))
            {
                var dirInfo = new DirectoryInfo(dir);
                items.Add(new FileBrowserItem
                {
                    Name = dirInfo.Name,
                    FullPath = dirInfo.FullName,
                    IsDirectory = true,
                    LastModified = dirInfo.LastWriteTime
                });
            }

            // Add files (filtered by extension if specified)
            foreach (var file in Directory.GetFiles(normalizedPath))
            {
                var fileInfo = new FileInfo(file);

                // Filter by extension if specified
                if (fileExtensions != null && fileExtensions.Length > 0)
                {
                    var ext = fileInfo.Extension.TrimStart('.').ToLowerInvariant();
                    if (!fileExtensions.Any(e => e.Equals(ext, StringComparison.OrdinalIgnoreCase)))
                        continue;
                }

                items.Add(new FileBrowserItem
                {
                    Name = fileInfo.Name,
                    FullPath = fileInfo.FullName,
                    IsDirectory = false,
                    Size = fileInfo.Length,
                    LastModified = fileInfo.LastWriteTime,
                    Extension = fileInfo.Extension
                });
            }

            // Sort: directories first, then by name
            items = items
                .OrderByDescending(i => i.IsDirectory)
                .ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            // Get parent path
            var parentDir = Directory.GetParent(normalizedPath);
            string? parentPath = null;
            if (parentDir != null && IsPathAllowed(parentDir.FullName))
            {
                parentPath = parentDir.FullName;
            }

            return Task.FromResult(new FileBrowserResult
            {
                CurrentPath = normalizedPath,
                ParentPath = parentPath,
                Items = items
            });
        }
        catch (UnauthorizedAccessException)
        {
            return Task.FromResult(new FileBrowserResult
            {
                CurrentPath = path,
                Items = [],
                ErrorMessage = "Access denied."
            });
        }
        catch (Exception ex)
        {
            return Task.FromResult(new FileBrowserResult
            {
                CurrentPath = path,
                Items = [],
                ErrorMessage = $"Error: {ex.Message}"
            });
        }
    }

    public Task<IReadOnlyList<FileBrowserItem>> GetRootPathsAsync()
    {
        var roots = new List<FileBrowserItem>();

        // Add configured input/output paths
        if (Directory.Exists(_inputPath))
        {
            roots.Add(new FileBrowserItem
            {
                Name = "Input",
                FullPath = Path.GetFullPath(_inputPath),
                IsDirectory = true
            });
        }

        if (Directory.Exists(_outputPath))
        {
            roots.Add(new FileBrowserItem
            {
                Name = "Output",
                FullPath = Path.GetFullPath(_outputPath),
                IsDirectory = true
            });
        }

        return Task.FromResult<IReadOnlyList<FileBrowserItem>>(roots);
    }

    public Task<bool> PathExistsAsync(string path, bool isDirectory = false)
    {
        var normalizedPath = Path.GetFullPath(path);

        if (!IsPathAllowed(normalizedPath))
            return Task.FromResult(false);

        return Task.FromResult(isDirectory
            ? Directory.Exists(normalizedPath)
            : File.Exists(normalizedPath));
    }

    private bool IsPathAllowed(string path)
    {
        // Only the configured input/output trees - the whole /data tree is NOT allowed,
        // the job database lives under /data/db
        return IsContainedIn(_inputPath, path) || IsContainedIn(_outputPath, path);
    }

    /// <summary>
    /// True when fullPath is the root itself or below it. Uses relative-path containment
    /// rather than StartsWith, which would accept siblings like /data/input-backup.
    /// </summary>
    private static bool IsContainedIn(string root, string fullPath)
    {
        var relative = Path.GetRelativePath(Path.GetFullPath(root), Path.GetFullPath(fullPath));

        return !Path.IsPathRooted(relative)
            && !relative.StartsWith("..", StringComparison.Ordinal);
    }
}
