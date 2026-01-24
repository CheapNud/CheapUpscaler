using System.Diagnostics;
using CheapUpscaler.Components.Services;

namespace CheapUpscaler.Blazor.Services;

/// <summary>
/// Desktop implementation of ISystemService with native file explorer integration.
/// </summary>
public class DesktopSystemService : ISystemService
{
    public bool SupportsExplorerIntegration => true;

    public Task OpenFolderInExplorerAsync(string path)
    {
        if (OperatingSystem.IsWindows())
            Process.Start("explorer.exe", path);
        else if (OperatingSystem.IsLinux())
            Process.Start("xdg-open", path);
        else if (OperatingSystem.IsMacOS())
            Process.Start("open", path);

        return Task.CompletedTask;
    }
}
