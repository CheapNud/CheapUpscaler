using System.Diagnostics;
using CheapAvaloniaBlazor.Extensions;
using CheapHelpers.MediaProcessing.Services;
using CheapUpscaler.Blazor.Data;
using CheapUpscaler.Blazor.Services;
using CheapUpscaler.Core;
using CheapUpscaler.Core.Services.RIFE;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MudBlazor;

namespace CheapUpscaler.Blazor;

class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        var builder = new CheapAvaloniaBlazor.Hosting.HostBuilder()
            .WithTitle("CheapUpscaler")
            .WithSize(1200, 800)
            .AddMudBlazor(config =>
            {
                config.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.BottomRight;
                config.SnackbarConfiguration.VisibleStateDuration = 2000;
                config.SnackbarConfiguration.ShowTransitionDuration = 200;
                config.SnackbarConfiguration.HideTransitionDuration = 200;
            });

        // Register CheapHelpers.MediaProcessing services (must be before Core services)
        builder.Services.AddSingleton<SvpDetectionService>();
        builder.Services.AddSingleton<HardwareDetectionService>();
        builder.Services.AddSingleton<ExecutableDetectionService>();

        // Register CheapUpscaler.Core AI services (depends on SvpDetectionService)
        builder.Services.AddUpscalerServices();

        // Override RIFE factory to check AppSettings before SVP auto-detection
        builder.Services.AddTransient(CreateRifeServiceWithSettings);

        // Register Blazor services
        builder.Services.AddSingleton<DependencyChecker>();
        builder.Services.AddSingleton<ISettingsService, SettingsService>();
        builder.Services.AddSingleton<IUpscaleProcessorService, UpscaleProcessorService>();
        builder.Services.AddSingleton<IVideoInfoService, VideoInfoService>();

        // Configure database (SQLite in AppData)
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dbDirectory = Path.Combine(appDataPath, "CheapUpscaler");
        Directory.CreateDirectory(dbDirectory);
        var dbPath = Path.Combine(dbDirectory, "upscaler.db");

        builder.Services.AddDbContextFactory<UpscaleJobDbContext>(options =>
            options.UseSqlite($"Data Source={dbPath}"));
        builder.Services.AddSingleton<IUpscaleJobRepository, UpscaleJobRepository>();

        // Register queue infrastructure
        builder.Services.AddSingleton<IBackgroundTaskQueue, BackgroundTaskQueue>();
        builder.Services.AddSingleton<UpscaleQueueService>();
        builder.Services.AddSingleton<IUpscaleQueueService>(sp => sp.GetRequiredService<UpscaleQueueService>());
        builder.Services.AddHostedService(sp => sp.GetRequiredService<UpscaleQueueService>());

        // Configure graceful shutdown
        builder.Services.Configure<HostOptions>(options =>
        {
            options.ShutdownTimeout = TimeSpan.FromSeconds(30);
        });

        // Ensure database is created
        EnsureDatabaseCreated(dbPath);

        builder.RunApp(args);
    }

    private static void EnsureDatabaseCreated(string dbPath)
    {
        var options = new DbContextOptionsBuilder<UpscaleJobDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;

        using var context = new UpscaleJobDbContext(options);
        context.Database.EnsureCreated();
    }

    /// <summary>
    /// Factory method to create RifeInterpolationService with settings-first path resolution.
    /// Priority: 1) AppSettings.ToolPaths.RifeFolderPath, 2) SVP auto-detection, 3) empty (RIFE unavailable)
    /// </summary>
    private static RifeInterpolationService CreateRifeServiceWithSettings(IServiceProvider serviceProvider)
    {
        var settings = serviceProvider.GetRequiredService<ISettingsService>();
        var svpDetection = serviceProvider.GetRequiredService<SvpDetectionService>();

        string rifePath;
        string pythonPath;

        // 1. Check AppSettings first (user-configured path)
        var configuredPath = settings.Settings.ToolPaths.RifeFolderPath;
        if (!string.IsNullOrEmpty(configuredPath))
        {
            if (Directory.Exists(configuredPath))
            {
                rifePath = configuredPath;
                pythonPath = settings.Settings.ToolPaths.PythonPath ?? "";
                Debug.WriteLine($"[RIFE] Using configured path from settings: {rifePath}");
                return new RifeInterpolationService(rifePath, pythonPath);
            }
            else
            {
                Debug.WriteLine($"[RIFE] WARNING: Configured path does not exist: {configuredPath}");
            }
        }

        // 2. Fall back to SVP auto-detection
        var svp = svpDetection.DetectSvpInstallation();
        if (svp.IsInstalled && !string.IsNullOrEmpty(svp.RifePath))
        {
            rifePath = svp.RifePath;
            pythonPath = !string.IsNullOrEmpty(svp.PythonPath) ? svp.PythonPath : "";
            Debug.WriteLine($"[RIFE] Using SVP installation: {rifePath}");
            return new RifeInterpolationService(rifePath, pythonPath);
        }

        // 3. RIFE not available - service will report IsConfigured=false
        Debug.WriteLine("[RIFE] WARNING: No RIFE installation found.");
        Debug.WriteLine("[RIFE] To enable RIFE: Install SVP 4 Pro (https://www.svp-team.com/get/) or configure RifeFolderPath in Settings.");
        return new RifeInterpolationService("", "");
    }
}
