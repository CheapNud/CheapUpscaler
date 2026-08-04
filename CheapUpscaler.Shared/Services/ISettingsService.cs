using CheapUpscaler.Shared.Models;

namespace CheapUpscaler.Shared.Services;

/// <summary>
/// Interface for settings service
/// </summary>
public interface ISettingsService
{
    AppSettings Settings { get; }
    event Action? SettingsChanged;
    Task<AppSettings> LoadAsync();
    Task SaveAsync();
    Task SaveAsync(AppSettings settings);
    Task ResetToDefaultsAsync();
    string GetSettingsFilePath();
}
