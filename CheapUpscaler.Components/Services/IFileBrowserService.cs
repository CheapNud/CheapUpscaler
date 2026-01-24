namespace CheapUpscaler.Components.Services;

/// <summary>
/// Service for browsing files and directories on the server.
/// Used by web mode when native file dialogs are not available.
/// </summary>
public interface IFileBrowserService
{
    /// <summary>
    /// Gets directory contents (folders and files).
    /// </summary>
    Task<FileBrowserResult> GetDirectoryContentsAsync(string path, string[]? fileExtensions = null);

    /// <summary>
    /// Gets the root/starting paths available for browsing.
    /// </summary>
    Task<IReadOnlyList<FileBrowserItem>> GetRootPathsAsync();

    /// <summary>
    /// Checks if a path exists on the server.
    /// </summary>
    Task<bool> PathExistsAsync(string path, bool isDirectory = false);
}

public record FileBrowserResult
{
    public required string CurrentPath { get; init; }
    public string? ParentPath { get; init; }
    public required IReadOnlyList<FileBrowserItem> Items { get; init; }
    public string? ErrorMessage { get; init; }
}

public record FileBrowserItem
{
    public required string Name { get; init; }
    public required string FullPath { get; init; }
    public required bool IsDirectory { get; init; }
    public long? Size { get; init; }
    public DateTime? LastModified { get; init; }
    public string? Extension { get; init; }
}
