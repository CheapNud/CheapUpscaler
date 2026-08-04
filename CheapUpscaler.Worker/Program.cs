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

// Kestrel limits for large video file uploads (50GB max)
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.Limits.MaxRequestBodySize = 50L * 1024 * 1024 * 1024;
});

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/worker-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// Add Blazor Server services with SignalR tuning for large file uploads
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddHubOptions(hubOptions =>
    {
        hubOptions.MaximumReceiveMessageSize = 1024 * 1024; // 1MB per SignalR message (default 32KB too small for file streaming)
        hubOptions.StreamBufferCapacity = 30; // Buffer up to 30 stream items
    });
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

// Core upscaler services (also registers platform-specific services)
builder.Services.AddUpscalerServices();

// Configure database path
var dataPath = builder.Configuration["Worker:DataPath"]
    ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CheapUpscaler");
Directory.CreateDirectory(dataPath);
var dbPath = Path.Combine(dataPath, "worker.db");

// Database (Cache=Shared enables connection pooling for concurrent access)
builder.Services.AddDbContextFactory<UpscaleJobDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath};Cache=Shared"));
builder.Services.AddSingleton<IUpscaleJobRepository, UpscaleJobRepository>();

// Queue infrastructure
builder.Services.AddSingleton<IBackgroundTaskQueue, BackgroundTaskQueue>();

// Worker services
builder.Services.AddSingleton<IUpscaleProcessor, WorkerProcessorService>();
builder.Services.AddSingleton(sp => new UpscaleQueueService(
    builder.Configuration.GetValue("Worker:MaxConcurrentJobs", 1),
    builder.Configuration.GetValue("Worker:AutoPauseWhenIdle", false),
    sp.GetRequiredService<IUpscaleProcessor>(),
    sp.GetRequiredService<IUpscaleJobRepository>(),
    sp.GetRequiredService<IBackgroundTaskQueue>(),
    sp.GetRequiredService<ILogger<UpscaleQueueService>>()));
builder.Services.AddSingleton<IUpscaleQueueService>(sp => sp.GetRequiredService<UpscaleQueueService>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<UpscaleQueueService>());

// Component services (Settings, VideoInfo, DependencyChecker, Hardware)
builder.Services.AddSingleton<ISettingsService, WorkerSettingsService>();
// Shared video metadata service - no host resolver, ToolProbe/PATH detection is right for Docker
builder.Services.AddSingleton<IVideoInfoService>(sp =>
    new VideoInfoService(sp.GetRequiredService<ILogger<VideoInfoService>>()));
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

// Apply pending migrations (creates the database on first run)
using (var scope = app.Services.CreateScope())
{
    var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<UpscaleJobDbContext>>();
    using var context = factory.CreateDbContext();
    context.Database.Migrate();
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
