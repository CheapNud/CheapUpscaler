using System.Diagnostics;
using System.Text.Json;
using CheapUpscaler.Shared.Models;
using CheapUpscaler.Shared.Services;

namespace CheapUpscaler.Blazor.Services;

/// <summary>
/// Service for loading and saving application settings
/// </summary>
public class SettingsService : ISettingsService
{
    private readonly string _settingsPath;
    private AppSettings _settings = new();

    public AppSettings Settings => _settings;

    public event Action? SettingsChanged;

    public SettingsService()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var settingsDir = Path.Combine(appDataPath, "CheapUpscaler");
        Directory.CreateDirectory(settingsDir);
        _settingsPath = Path.Combine(settingsDir, "settings.json");

        // Load settings synchronously in constructor so Settings is never an unloaded default
        if (File.Exists(_settingsPath))
        {
            try
            {
                var json = File.ReadAllText(_settingsPath);
                _settings = JsonSerializer.Deserialize<AppSettings>(json, AppSettings.JsonOptions) ?? new AppSettings();
                Debug.WriteLine($"Settings loaded from {_settingsPath}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading settings: {ex.Message}");
                _settings = new AppSettings();
            }
        }
        else
        {
            Debug.WriteLine("Using default settings (no settings file found)");
        }
    }

    public async Task<AppSettings> LoadAsync()
    {
        if (File.Exists(_settingsPath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(_settingsPath);
                _settings = JsonSerializer.Deserialize<AppSettings>(json, AppSettings.JsonOptions) ?? new AppSettings();
                Debug.WriteLine($"Settings loaded from {_settingsPath}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading settings: {ex.Message}");
                _settings = new AppSettings();
            }
        }
        else
        {
            _settings = new AppSettings();
            Debug.WriteLine("Using default settings (no settings file found)");
        }

        return _settings;
    }

    public async Task SaveAsync()
    {
        try
        {
            var json = JsonSerializer.Serialize(_settings, AppSettings.JsonOptions);
            await File.WriteAllTextAsync(_settingsPath, json);
            Debug.WriteLine($"Settings saved to {_settingsPath}");
            SettingsChanged?.Invoke();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error saving settings: {ex.Message}");
            throw;
        }
    }

    public async Task SaveAsync(AppSettings settings)
    {
        _settings = settings;
        await SaveAsync();
    }

    public async Task ResetToDefaultsAsync()
    {
        _settings = new AppSettings();
        await SaveAsync();
    }

    public string GetSettingsFilePath() => _settingsPath;
}

