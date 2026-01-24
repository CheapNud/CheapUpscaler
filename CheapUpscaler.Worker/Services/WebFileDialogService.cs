using CheapUpscaler.Components.Services;

namespace CheapUpscaler.Worker.Services;

/// <summary>
/// Web implementation of IFileDialogService.
/// Native dialogs are not supported in browser; UI shows manual path entry instead.
/// </summary>
public class WebFileDialogService : IFileDialogService
{
    public bool SupportsNativeDialogs => false;

    public Task<string?> OpenFileAsync(FileDialogOptions options)
        => Task.FromResult<string?>(null);

    public Task<string?> SaveFileAsync(FileDialogOptions options)
        => Task.FromResult<string?>(null);

    public Task<string?> OpenFolderAsync(string? title = null)
        => Task.FromResult<string?>(null);
}
