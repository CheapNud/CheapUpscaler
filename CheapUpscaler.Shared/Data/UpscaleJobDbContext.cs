using System.Data.Common;
using CheapUpscaler.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CheapUpscaler.Shared.Data;

/// <summary>
/// Database context for upscale job persistence
/// </summary>
public class UpscaleJobDbContext(DbContextOptions<UpscaleJobDbContext> options) : DbContext(options)
{
    public DbSet<UpscaleJobEntity> Jobs { get; set; } = null!;

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // Enable SQLite WAL mode and busy timeout via connection interceptor
        optionsBuilder.AddInterceptors(new SqliteWalInterceptor());
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UpscaleJobEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.JobId).IsUnique();
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.CreatedAt);

            entity.Property(e => e.JobId).IsRequired();
            entity.Property(e => e.JobName).HasMaxLength(256);
            entity.Property(e => e.SourceVideoPath).IsRequired().HasMaxLength(1024);
            entity.Property(e => e.OutputPath).IsRequired().HasMaxLength(1024);
            entity.Property(e => e.SettingsJson).HasMaxLength(4096);
            entity.Property(e => e.LastError).HasMaxLength(2048);
            entity.Property(e => e.ErrorStackTrace).HasMaxLength(8192);
            entity.Property(e => e.MachineName).HasMaxLength(256);
        });
    }
}

/// <summary>
/// Database entity for upscale jobs (separate from UpscaleJob to allow EF tracking)
/// </summary>
public class UpscaleJobEntity
{
    public int Id { get; set; }
    public Guid JobId { get; set; }
    public string? JobName { get; set; }

    // Source & Output
    public string SourceVideoPath { get; set; } = string.Empty;
    public string OutputPath { get; set; } = string.Empty;

    // Upscale Configuration
    public UpscaleType UpscaleType { get; set; }
    public string SettingsJson { get; set; } = "{}";

    // Status & Progress
    public UpscaleJobStatus Status { get; set; }
    public double ProgressPercentage { get; set; }
    public int CurrentFrame { get; set; }
    public int? TotalFrames { get; set; }
    public long? EstimatedTimeRemainingTicks { get; set; }

    // Timestamps
    public DateTime CreatedAt { get; set; }
    public DateTime? QueuedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime LastUpdatedAt { get; set; }

    // Error handling
    public string? LastError { get; set; }
    public string? ErrorStackTrace { get; set; }
    public int RetryCount { get; set; }
    public int MaxRetries { get; set; } = 3;

    // Processing info
    public int? ProcessId { get; set; }
    public string? MachineName { get; set; }

    // Source video info
    public int? SourceWidth { get; set; }
    public int? SourceHeight { get; set; }
    public double? SourceFps { get; set; }
    public long? SourceDurationTicks { get; set; }

    // Output info
    public int? OutputWidth { get; set; }
    public int? OutputHeight { get; set; }
    public double? OutputFps { get; set; }
    public long? OutputFileSizeBytes { get; set; }

    /// <summary>
    /// Convert entity to domain model
    /// </summary>
    public UpscaleJob ToModel() => new()
    {
        Id = Id,
        JobId = JobId,
        JobName = JobName,
        SourceVideoPath = SourceVideoPath,
        OutputPath = OutputPath,
        UpscaleType = UpscaleType,
        SettingsJson = SettingsJson,
        Status = Status,
        ProgressPercentage = ProgressPercentage,
        CurrentFrame = CurrentFrame,
        TotalFrames = TotalFrames,
        EstimatedTimeRemaining = EstimatedTimeRemainingTicks.HasValue
            ? TimeSpan.FromTicks(EstimatedTimeRemainingTicks.Value)
            : null,
        CreatedAt = CreatedAt,
        QueuedAt = QueuedAt,
        StartedAt = StartedAt,
        CompletedAt = CompletedAt,
        LastUpdatedAt = LastUpdatedAt,
        LastError = LastError,
        ErrorStackTrace = ErrorStackTrace,
        RetryCount = RetryCount,
        MaxRetries = MaxRetries,
        ProcessId = ProcessId,
        MachineName = MachineName,
        SourceWidth = SourceWidth,
        SourceHeight = SourceHeight,
        SourceFps = SourceFps,
        SourceDuration = SourceDurationTicks.HasValue
            ? TimeSpan.FromTicks(SourceDurationTicks.Value)
            : null,
        OutputWidth = OutputWidth,
        OutputHeight = OutputHeight,
        OutputFps = OutputFps,
        OutputFileSizeBytes = OutputFileSizeBytes
    };

    /// <summary>
    /// Create entity from domain model
    /// </summary>
    public static UpscaleJobEntity FromModel(UpscaleJob job)
    {
        // Identity/creation fields are set once here; everything else goes through UpdateFrom
        // so the insert and update paths can never drift apart.
        var entity = new UpscaleJobEntity
        {
            Id = job.Id,
            JobId = job.JobId,
            CreatedAt = job.CreatedAt
        };
        entity.UpdateFrom(job);
        return entity;
    }

    /// <summary>
    /// Update entity from domain model (all mutable fields; Id, JobId and CreatedAt are preserved)
    /// </summary>
    public void UpdateFrom(UpscaleJob job)
    {
        JobName = job.JobName;
        SourceVideoPath = job.SourceVideoPath;
        OutputPath = job.OutputPath;
        UpscaleType = job.UpscaleType;
        SettingsJson = job.SettingsJson;
        Status = job.Status;
        ProgressPercentage = job.ProgressPercentage;
        CurrentFrame = job.CurrentFrame;
        TotalFrames = job.TotalFrames;
        EstimatedTimeRemainingTicks = job.EstimatedTimeRemaining?.Ticks;
        QueuedAt = job.QueuedAt;
        StartedAt = job.StartedAt;
        CompletedAt = job.CompletedAt;
        LastUpdatedAt = job.LastUpdatedAt;
        LastError = job.LastError;
        ErrorStackTrace = job.ErrorStackTrace;
        RetryCount = job.RetryCount;
        MaxRetries = job.MaxRetries;
        ProcessId = job.ProcessId;
        MachineName = job.MachineName;
        SourceWidth = job.SourceWidth;
        SourceHeight = job.SourceHeight;
        SourceFps = job.SourceFps;
        SourceDurationTicks = job.SourceDuration?.Ticks;
        OutputWidth = job.OutputWidth;
        OutputHeight = job.OutputHeight;
        OutputFps = job.OutputFps;
        OutputFileSizeBytes = job.OutputFileSizeBytes;
    }
}

/// <summary>
/// Interceptor that enables WAL journal mode and busy timeout on every SQLite connection.
/// WAL allows concurrent reads during writes; busy_timeout retries instead of failing immediately.
/// </summary>
internal sealed class SqliteWalInterceptor : DbConnectionInterceptor
{
    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        if (connection is Microsoft.Data.Sqlite.SqliteConnection)
        {
            using var pragmaCmd = connection.CreateCommand();
            pragmaCmd.CommandText = """
                PRAGMA journal_mode=WAL;
                PRAGMA busy_timeout=5000;
                PRAGMA synchronous=NORMAL;
                """;
            pragmaCmd.ExecuteNonQuery();
        }
    }

    public override async Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        if (connection is Microsoft.Data.Sqlite.SqliteConnection)
        {
            await using var pragmaCmd = connection.CreateCommand();
            pragmaCmd.CommandText = """
                PRAGMA journal_mode=WAL;
                PRAGMA busy_timeout=5000;
                PRAGMA synchronous=NORMAL;
                """;
            await pragmaCmd.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}
