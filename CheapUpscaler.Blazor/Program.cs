using CheapAvaloniaBlazor.Extensions;
using CheapHelpers.MediaProcessing.Services;
using CheapUpscaler.Blazor.Services;
using CheapUpscaler.Components.Services;
using CheapUpscaler.Core;
using CheapUpscaler.Shared.Data;
using CheapUpscaler.Shared.Services;
using CheapUpscaler.Core.Services.RIFE;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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
        builder.Services.AddSingleton<IDependencyChecker>(sp => sp.GetRequiredService<DependencyChecker>());
        builder.Services.AddSingleton<ISettingsService, SettingsService>();
        builder.Services.AddSingleton<IUpscaleProcessor, UpscaleProcessorService>();
        // Shared video metadata service, resolving ffmpeg through desktop (SVP-aware) detection
        builder.Services.AddSingleton<IVideoInfoService>(sp =>
        {
            var executableDetection = sp.GetRequiredService<ExecutableDetectionService>();
            return new VideoInfoService(
                sp.GetRequiredService<ILogger<VideoInfoService>>(),
                () => executableDetection.DetectFFmpeg(useSvpEncoders: false, customPath: null));
        });

        // Register platform-specific services for Components
        builder.Services.AddScoped<IFileDialogService, DesktopFileDialogService>();
        builder.Services.AddScoped<ISystemService, DesktopSystemService>();
        builder.Services.AddSingleton<IHardwareService, DesktopHardwareService>();
        builder.Services.AddScoped<IFileUploadService, DesktopUploadService>();
        builder.Services.AddScoped<IFileBrowserService, DesktopFileBrowserService>();

        // Configure database (SQLite in AppData)
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dbDirectory = Path.Combine(appDataPath, "CheapUpscaler");
        Directory.CreateDirectory(dbDirectory);
        var dbPath = Path.Combine(dbDirectory, "upscaler.db");

        builder.Services.AddDbContextFactory<UpscaleJobDbContext>(options =>
            options.UseSqlite($"Data Source={dbPath};Cache=Shared"));
        builder.Services.AddSingleton<IUpscaleJobRepository, UpscaleJobRepository>();

        // Register queue infrastructure
        builder.Services.AddSingleton<IBackgroundTaskQueue, BackgroundTaskQueue>();
        builder.Services.AddSingleton(sp => new UpscaleQueueService(
            sp.GetRequiredService<ISettingsService>().Settings.Queue.MaxConcurrentJobs,
            autoPauseWhenIdle: false,
            sp.GetRequiredService<IUpscaleProcessor>(),
            sp.GetRequiredService<IUpscaleJobRepository>(),
            sp.GetRequiredService<IBackgroundTaskQueue>(),
            sp.GetRequiredService<ILogger<UpscaleQueueService>>()));
        builder.Services.AddSingleton<IUpscaleQueueService>(sp => sp.GetRequiredService<UpscaleQueueService>());
        builder.Services.AddHostedService(sp => sp.GetRequiredService<UpscaleQueueService>());

        // Configure graceful shutdown
        builder.Services.Configure<HostOptions>(options =>
        {
            options.ShutdownTimeout = TimeSpan.FromSeconds(30);
        });

        // Apply pending migrations (creates the database on first run)
        MigrateDatabase(dbPath);

        builder.RunApp(args);
    }

    private static void MigrateDatabase(string dbPath)
    {
        var options = new DbContextOptionsBuilder<UpscaleJobDbContext>()
            .UseSqlite($"Data Source={dbPath};Cache=Shared")
            .Options;

        using var context = new UpscaleJobDbContext(options);
        context.Database.Migrate();
    }

    /// <summary>
    /// Factory method to create RifeInterpolationService with settings-first path resolution.
    /// Uses shared ResolveRifePaths from Core with AppSettings values.
    /// </summary>
    private static RifeInterpolationService CreateRifeServiceWithSettings(IServiceProvider serviceProvider)
    {
        var settings = serviceProvider.GetRequiredService<ISettingsService>();
        var svpDetection = serviceProvider.GetRequiredService<SvpDetectionService>();

        var (rifePath, pythonPath) = Core.ServiceCollectionExtensions.ResolveRifePaths(
            settings.Settings.ToolPaths.RifeFolderPath,
            settings.Settings.ToolPaths.PythonPath,
            svpDetection);

        return new RifeInterpolationService(rifePath, pythonPath);
    }
}
