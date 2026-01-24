using CheapUpscaler.Components.Services;

namespace CheapUpscaler.Worker.Services;

/// <summary>
/// Web implementation of ISystemService.
/// File explorer integration is not available in browser context.
/// </summary>
public class WebSystemService : ISystemService
{
    public bool SupportsExplorerIntegration => false;

    public Task OpenFolderInExplorerAsync(string path)
        => Task.CompletedTask; // No-op in browser
}
