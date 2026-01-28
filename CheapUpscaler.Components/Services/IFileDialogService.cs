namespace CheapUpscaler.Components.Services;

/// <summary>
/// Platform abstraction for file and folder dialogs.
/// Desktop implementations use native OS dialogs; web implementations return null (UI shows text inputs instead).
/// </summary>
public interface IFileDialogService
{
    /// <summary>
    /// Opens a file selection dialog.
    /// </summary>
    /// <param name="options">Dialog configuration options.</param>
    /// <returns>Selected file path, or null if cancelled or unsupported.</returns>
    Task<string?> OpenFileAsync(FileDialogOptions options);

    /// <summary>
    /// Opens a save file dialog.
    /// </summary>
    /// <param name="options">Dialog configuration options.</param>
    /// <returns>Selected save path, or null if cancelled or unsupported.</returns>
    Task<string?> SaveFileAsync(FileDialogOptions options);

    /// <summary>
    /// Opens a folder selection dialog.
    /// </summary>
    /// <param name="title">Optional dialog title.</param>
    /// <returns>Selected folder path, or null if cancelled or unsupported.</returns>
    Task<string?> OpenFolderAsync(string? title = null);

    /// <summary>
    /// Indicates whether this platform supports native file dialogs.
    /// When false, UI should show manual path entry fields instead of browse buttons.
    /// </summary>
    bool SupportsNativeDialogs { get; }
}

/// <summary>
/// Configuration options for file dialogs.
/// </summary>
public record FileDialogOptions
{
    /// <summary>
    /// Dialog window title.
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// File extensions to filter (without dots), e.g., ["mp4", "mkv"].
    /// </summary>
    public string[]? Extensions { get; init; }

    /// <summary>
    /// Default filename for save dialogs.
    /// </summary>
    public string? DefaultFileName { get; init; }
}
