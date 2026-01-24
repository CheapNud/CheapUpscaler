namespace CheapUpscaler.Components.Services;

/// <summary>
/// Platform abstraction for system integration features.
/// Desktop implementations can launch file explorer; web implementations are no-ops.
/// </summary>
public interface ISystemService
{
    /// <summary>
    /// Opens the system file explorer/finder to the specified folder path.
    /// </summary>
    /// <param name="path">The folder path to open.</param>
    Task OpenFolderInExplorerAsync(string path);

    /// <summary>
    /// Indicates whether this platform supports file explorer integration.
    /// When false, UI should hide "Open folder" buttons.
    /// </summary>
    bool SupportsExplorerIntegration { get; }
}
