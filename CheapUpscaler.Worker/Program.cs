using CheapHelpers.MediaProcessing;
using CheapUpscaler.Components.Services;
using CheapUpscaler.Core;
using CheapUpscaler.Shared.Data;
using CheapUpscaler.Shared.Services;
using CheapUpscaler.Worker.Components;
using CheapUpscaler.Worker.Services;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/worker-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// Add Blazor Server services
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddMudServices();

// Register web implementations for platform abstractions
builder.Services.AddScoped<IFileDialogService, WebFileDialogService>();
builder.Services.AddScoped<ISystemService, WebSystemService>();
builder.Services.AddScoped<IFileBrowserService, ServerFileBrowserService>();
builder.Services.AddScoped<IFileUploadService, ServerFileUploadService>();

// Add API services
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// CheapHelpers.MediaProcessing services (platform-aware)
builder.Services.AddMediaProcessing();

// Platform-specific services (auto-detects Windows vs Linux)
builder.Services.AddPlatformServices();

// Core upscaler services
builder.Services.AddUpscalerServices();

// Configure database path
var dataPath = builder.Configuration["Worker:DataPath"]
    ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CheapUpscaler");
Directory.CreateDirectory(dataPath);
var dbPath = Path.Combine(dataPath, "worker.db");

// Database
builder.Services.AddDbContextFactory<UpscaleJobDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));
builder.Services.AddSingleton<IUpscaleJobRepository, UpscaleJobRepository>();

// Queue infrastructure
builder.Services.AddSingleton<IBackgroundTaskQueue, BackgroundTaskQueue>();

// Worker services
builder.Services.AddSingleton<IWorkerProcessorService, WorkerProcessorService>();
builder.Services.AddSingleton<WorkerQueueService>();
builder.Services.AddSingleton<IUpscaleQueueService>(sp => sp.GetRequiredService<WorkerQueueService>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<WorkerQueueService>());

// Component services (Settings, VideoInfo, DependencyChecker, Hardware)
builder.Services.AddSingleton<ISettingsService, WorkerSettingsService>();
builder.Services.AddSingleton<IVideoInfoService, WorkerVideoInfoService>();
builder.Services.AddSingleton<IDependencyChecker, WorkerDependencyChecker>();
builder.Services.AddSingleton<IHardwareService, WorkerHardwareService>();

// File watcher (optional)
var watchFolderEnabled = builder.Configuration.GetValue<bool>("Worker:WatchFolderEnabled", false);
if (watchFolderEnabled)
{
    builder.Services.AddHostedService<FileWatcherService>();
}

// Configure graceful shutdown
builder.Services.Configure<HostOptions>(options =>
{
    options.ShutdownTimeout = TimeSpan.FromSeconds(30);
});

var app = builder.Build();

// Ensure database is created
using (var scope = app.Services.CreateScope())
{
    var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<UpscaleJobDbContext>>();
    using var context = factory.CreateDbContext();
    context.Database.EnsureCreated();
}

// Configure middleware
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "CheapUpscaler Worker API v1");
    });
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();
app.UseAuthorization();

// Map API controllers
app.MapControllers();

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Timestamp = DateTime.UtcNow }));

// Map Blazor components (include Components library for routable pages)
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(typeof(CheapUpscaler.Components.Pages.Home).Assembly);

Log.Information("CheapUpscaler Worker starting...");
Log.Information("Data path: {DataPath}", dataPath);
Log.Information("Watch folder enabled: {WatchFolderEnabled}", watchFolderEnabled);

app.Run();
