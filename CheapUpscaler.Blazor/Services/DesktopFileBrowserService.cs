using CheapUpscaler.Components.Services;

namespace CheapUpscaler.Blazor.Services;

/// <summary>
/// Desktop file browser service that browses the local Windows filesystem.
/// Used by shared components that need server-side file browsing (e.g. AddUpscaleJobDialog).
/// On desktop, native file dialogs are preferred, but this provides fallback browsing.
/// </summary>
public class DesktopFileBrowserService : IFileBrowserService
{
    public Task<FileBrowserResult> GetDirectoryContentsAsync(string path, string[]? fileExtensions = null)
    {
        try
        {
            var normalizedPath = Path.GetFullPath(path);

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

            foreach (var file in Directory.GetFiles(normalizedPath))
            {
                var fileInfo = new FileInfo(file);

                if (fileExtensions is { Length: > 0 })
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

            items = items
                .OrderByDescending(i => i.IsDirectory)
                .ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var parentDir = Directory.GetParent(normalizedPath);

            return Task.FromResult(new FileBrowserResult
            {
                CurrentPath = normalizedPath,
                ParentPath = parentDir?.FullName,
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

        foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady))
        {
            roots.Add(new FileBrowserItem
            {
                Name = $"{drive.Name.TrimEnd('\\')} ({drive.VolumeLabel})",
                FullPath = drive.RootDirectory.FullName,
                IsDirectory = true
            });
        }

        // Add common user folders
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (Directory.Exists(userProfile))
        {
            roots.Add(new FileBrowserItem
            {
                Name = "User Profile",
                FullPath = userProfile,
                IsDirectory = true
            });
        }

        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        if (Directory.Exists(desktop))
        {
            roots.Add(new FileBrowserItem
            {
                Name = "Desktop",
                FullPath = desktop,
                IsDirectory = true
            });
        }

        return Task.FromResult<IReadOnlyList<FileBrowserItem>>(roots);
    }

    public Task<bool> PathExistsAsync(string path, bool isDirectory = false)
    {
        var normalizedPath = Path.GetFullPath(path);
        return Task.FromResult(isDirectory
            ? Directory.Exists(normalizedPath)
            : File.Exists(normalizedPath));
    }
}
