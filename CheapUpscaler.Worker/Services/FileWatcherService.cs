using System.Text.Json;
using CheapUpscaler.Shared.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CheapUpscaler.Worker.Services;

/// <summary>
/// Watches configured folders for new video files and automatically queues them for processing
/// </summary>
public class FileWatcherService : BackgroundService
{
    private readonly WorkerQueueService _queueService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<FileWatcherService> _logger;
    private readonly List<FileSystemWatcher> _watchers = [];
    private readonly HashSet<string> _processedFiles = [];
    private readonly object _processedFilesLock = new();

    // Supported video extensions
    private static readonly HashSet<string> VideoExtensions =
    [
        ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".flv", ".webm", ".m4v", ".mpeg", ".mpg"
    ];

    public FileWatcherService(
        WorkerQueueService queueService,
        IConfiguration configuration,
        ILogger<FileWatcherService> logger)
    {
        _queueService = queueService;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var inputPath = _configuration["Worker:InputPath"] ?? "/data/input";
        var outputPath = _configuration["Worker:OutputPath"] ?? "/data/output";

        if (!Directory.Exists(inputPath))
        {
            _logger.LogWarning("Watch folder does not exist: {InputPath}. Creating...", inputPath);
            Directory.CreateDirectory(inputPath);
        }

        if (!Directory.Exists(outputPath))
        {
            _logger.LogInformation("Creating output folder: {OutputPath}", outputPath);
            Directory.CreateDirectory(outputPath);
        }

        _logger.LogInformation("FileWatcherService starting - watching: {InputPath}", inputPath);

        // Process existing files first
        await ProcessExistingFilesAsync(inputPath, outputPath, stoppingToken);

        // Set up file watcher
        var watcher = new FileSystemWatcher(inputPath)
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.CreationTime,
            EnableRaisingEvents = true
        };

        watcher.Created += async (sender, e) => await OnFileCreatedAsync(e.FullPath, outputPath);
        watcher.Renamed += async (sender, e) => await OnFileCreatedAsync(e.FullPath, outputPath);

        _watchers.Add(watcher);

        _logger.LogInformation("FileWatcherService started");

        // Keep the service running
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }

        // Cleanup
        foreach (var w in _watchers)
        {
            w.EnableRaisingEvents = false;
            w.Dispose();
        }

        _logger.LogInformation("FileWatcherService stopped");
    }

    private async Task ProcessExistingFilesAsync(string inputPath, string outputPath, CancellationToken cancellationToken)
    {
        try
        {
            var existingFiles = Directory.GetFiles(inputPath)
                .Where(f => VideoExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .ToList();

            _logger.LogInformation("Found {Count} existing video files in watch folder", existingFiles.Count);

            foreach (var file in existingFiles)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                await QueueFileForProcessingAsync(file, outputPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing existing files");
        }
    }

    private async Task OnFileCreatedAsync(string filePath, string outputPath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        if (!VideoExtensions.Contains(extension))
        {
            return;
        }

        // Wait a bit for file to be fully written
        await Task.Delay(TimeSpan.FromSeconds(2));

        // Check if file is still being written
        if (!await WaitForFileReadyAsync(filePath))
        {
            _logger.LogWarning("File {FilePath} is not accessible, skipping", filePath);
            return;
        }

        await QueueFileForProcessingAsync(filePath, outputPath);
    }

    private async Task QueueFileForProcessingAsync(string inputFile, string outputPath)
    {
        lock (_processedFilesLock)
        {
            if (_processedFiles.Contains(inputFile))
            {
                return;
            }
            _processedFiles.Add(inputFile);
        }

        var fileName = Path.GetFileNameWithoutExtension(inputFile);
        var outputFile = Path.Combine(outputPath, $"{fileName}_upscaled.mp4");

        // Check if output already exists
        if (File.Exists(outputFile))
        {
            _logger.LogInformation("Output already exists for {FileName}, skipping", fileName);
            return;
        }

        // Check if a job already exists for this input file
        var existingJobs = await _queueService.GetAllJobsAsync();
        if (existingJobs.Any(j => j.SourceVideoPath == inputFile &&
            j.Status is UpscaleJobStatus.Pending or UpscaleJobStatus.Running or UpscaleJobStatus.Paused))
        {
            _logger.LogInformation("Job already exists for {FileName}, skipping auto-queue", fileName);
            return;
        }

        // Get default settings from configuration
        var defaultType = _configuration["Worker:DefaultUpscaleType"] ?? "RealEsrgan";
        var upscaleType = Enum.TryParse<UpscaleType>(defaultType, true, out var type)
            ? type
            : UpscaleType.RealEsrgan;

        var settings = GetDefaultSettings(upscaleType);

        var job = new UpscaleJob
        {
            JobId = Guid.NewGuid(),
            JobName = $"Auto: {fileName}",
            SourceVideoPath = inputFile,
            OutputPath = outputFile,
            UpscaleType = upscaleType,
            SettingsJson = JsonSerializer.Serialize(settings),
            CreatedAt = DateTime.UtcNow
        };

        await _queueService.AddJobAsync(job);
        _logger.LogInformation("Queued auto-job for {FileName} ({UpscaleType})", fileName, upscaleType);
    }

    private object GetDefaultSettings(UpscaleType upscaleType)
    {
        return upscaleType switch
        {
            UpscaleType.Rife => new RifeJobSettings
            {
                Multiplier = _configuration.GetValue("Worker:Defaults:Rife:Multiplier", 2),
                QualityPreset = _configuration["Worker:Defaults:Rife:QualityPreset"] ?? "Medium"
            },
            UpscaleType.RealCugan => new RealCuganJobSettings
            {
                Scale = _configuration.GetValue("Worker:Defaults:RealCugan:Scale", 2),
                NoiseLevel = _configuration.GetValue("Worker:Defaults:RealCugan:NoiseLevel", -1),
                UseFp16 = _configuration.GetValue("Worker:Defaults:RealCugan:UseFp16", true)
            },
            UpscaleType.RealEsrgan => new RealEsrganJobSettings
            {
                Model = _configuration["Worker:Defaults:RealEsrgan:Model"] ?? "realesrgan-x4plus-anime",
                Scale = _configuration.GetValue("Worker:Defaults:RealEsrgan:Scale", 4),
                TileSize = _configuration.GetValue("Worker:Defaults:RealEsrgan:TileSize", 0),
                UseFp16 = _configuration.GetValue("Worker:Defaults:RealEsrgan:UseFp16", true)
            },
            UpscaleType.NonAi => new NonAiJobSettings
            {
                Algorithm = _configuration["Worker:Defaults:NonAi:Algorithm"] ?? "lanczos",
                Scale = _configuration.GetValue("Worker:Defaults:NonAi:Scale", 2)
            },
            _ => new RealEsrganJobSettings()
        };
    }

    private static async Task<bool> WaitForFileReadyAsync(string filePath, int maxRetries = 10)
    {
        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.None);
                return true;
            }
            catch (IOException)
            {
                await Task.Delay(TimeSpan.FromSeconds(1));
            }
        }
        return false;
    }
}
