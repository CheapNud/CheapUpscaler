using CheapAvaloniaBlazor.Extensions;
using CheapHelpers.MediaProcessing.Services;
using CheapUpscaler.Blazor.Services;
using CheapUpscaler.Core;
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

        // Register CheapUpscaler.Core AI services
        builder.Services.AddUpscalerServices();

        // Register CheapHelpers.MediaProcessing services
        builder.Services.AddSingleton<SvpDetectionService>();
        builder.Services.AddSingleton<HardwareDetectionService>();
        builder.Services.AddSingleton<ExecutableDetectionService>();

        // Register Blazor services
        builder.Services.AddSingleton<DependencyChecker>();
        builder.Services.AddSingleton<ISettingsService, SettingsService>();

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

        builder.RunApp(args);
    }
}
