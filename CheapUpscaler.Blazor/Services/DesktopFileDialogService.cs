using CheapAvaloniaBlazor.Services;
using CheapUpscaler.Components.Services;

namespace CheapUpscaler.Blazor.Services;

/// <summary>
/// Desktop implementation of IFileDialogService using CheapAvaloniaBlazor native dialogs.
/// </summary>
public class DesktopFileDialogService(IDesktopInteropService desktopInterop) : IFileDialogService
{
    public bool SupportsNativeDialogs => true;

    public Task<string?> OpenFileAsync(FileDialogOptions options)
        => desktopInterop.OpenFileDialogAsync();

    public Task<string?> SaveFileAsync(FileDialogOptions options)
        => desktopInterop.SaveFileDialogAsync();

    public Task<string?> OpenFolderAsync(string? title = null)
        => desktopInterop.OpenFolderDialogAsync();
}
