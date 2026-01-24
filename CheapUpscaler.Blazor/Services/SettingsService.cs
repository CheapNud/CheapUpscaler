using System.Diagnostics;
using System.Text.Json;
using CheapUpscaler.Components.Models;
using CheapUpscaler.Components.Services;

namespace CheapUpscaler.Blazor.Services;

/// <summary>
/// Service for loading and saving application settings
/// </summary>
public class SettingsService : ISettingsService
{
    private readonly string _settingsPath;
    private readonly JsonSerializerOptions _jsonOptions;
    private AppSettings _settings = new();
    private bool _isLoaded;

    public AppSettings Settings => _settings;

    public event Action? SettingsChanged;

    public SettingsService()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var settingsDir = Path.Combine(appDataPath, "CheapUpscaler");
        Directory.CreateDirectory(settingsDir);
        _settingsPath = Path.Combine(settingsDir, "settings.json");

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public async Task<AppSettings> LoadAsync()
    {
        if (_isLoaded)
            return _settings;

        try
        {
            if (File.Exists(_settingsPath))
            {
                var json = await File.ReadAllTextAsync(_settingsPath);
                _settings = JsonSerializer.Deserialize<AppSettings>(json, _jsonOptions) ?? new AppSettings();
                Debug.WriteLine($"Settings loaded from {_settingsPath}");
            }
            else
            {
                _settings = new AppSettings();
                Debug.WriteLine("Using default settings (no settings file found)");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error loading settings: {ex.Message}");
            _settings = new AppSettings();
        }

        _isLoaded = true;
        return _settings;
    }

    public async Task SaveAsync()
    {
        try
        {
            var json = JsonSerializer.Serialize(_settings, _jsonOptions);
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

