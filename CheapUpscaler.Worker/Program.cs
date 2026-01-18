using CheapUpscaler.Core;
using CheapUpscaler.Shared.Data;
using CheapUpscaler.Shared.Services;
using CheapUpscaler.Worker.Services;
using Microsoft.EntityFrameworkCore;
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

// Add services
builder.Services.AddControllers();
builder.Services.AddOpenApi();

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
builder.Services.AddHostedService(sp => sp.GetRequiredService<WorkerQueueService>());

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
app.UseAuthorization();
app.MapControllers();

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Timestamp = DateTime.UtcNow }));

Log.Information("CheapUpscaler Worker starting...");
Log.Information("Data path: {DataPath}", dataPath);
Log.Information("Watch folder enabled: {WatchFolderEnabled}", watchFolderEnabled);

app.Run();
