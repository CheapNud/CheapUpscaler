using System.Text.Json;
using CheapUpscaler.Components.Models;
using CheapUpscaler.Components.Services;

namespace CheapUpscaler.Worker.Services;

/// <summary>
/// Settings service for the Worker (Docker/headless mode)
/// Uses JSON file storage in the data directory
/// </summary>
public class WorkerSettingsService : ISettingsService
{
    private readonly string _settingsPath;
    private AppSettings _settings = new();

    public event Action? SettingsChanged;

    public AppSettings Settings => _settings;

    public WorkerSettingsService(IConfiguration configuration)
    {
        var dataPath = configuration["Worker:DataPath"]
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CheapUpscaler");
        Directory.CreateDirectory(dataPath);
        _settingsPath = Path.Combine(dataPath, "worker-settings.json");

        // Load settings synchronously in constructor
        if (File.Exists(_settingsPath))
        {
            try
            {
                var json = File.ReadAllText(_settingsPath);
                _settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
            catch
            {
                _settings = new AppSettings();
            }
        }
    }

    public async Task<AppSettings> LoadAsync()
    {
        if (File.Exists(_settingsPath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(_settingsPath);
                _settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
            catch
            {
                _settings = new AppSettings();
            }
        }
        else
        {
            _settings = new AppSettings();
        }

        SettingsChanged?.Invoke();
        return _settings;
    }

    public async Task SaveAsync()
    {
        await SaveAsync(_settings);
    }

    public async Task SaveAsync(AppSettings settings)
    {
        _settings = settings;
        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_settingsPath, json);
        SettingsChanged?.Invoke();
    }

    public async Task ResetToDefaultsAsync()
    {
        _settings = new AppSettings();
        await SaveAsync(_settings);
    }

    public string GetSettingsFilePath() => _settingsPath;
}
