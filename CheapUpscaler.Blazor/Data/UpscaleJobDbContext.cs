using CheapUpscaler.Blazor.Models;
using Microsoft.EntityFrameworkCore;

namespace CheapUpscaler.Blazor.Data;

/// <summary>
/// Database context for upscale job persistence
/// </summary>
public class UpscaleJobDbContext(DbContextOptions<UpscaleJobDbContext> options) : DbContext(options)
{
    public DbSet<UpscaleJobEntity> Jobs { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UpscaleJobEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.JobId).IsUnique();
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.CreatedAt);

            entity.Property(e => e.JobId).IsRequired();
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

    /// <summary>
    /// Convert entity to domain model
    /// </summary>
    public UpscaleJob ToModel() => new()
    {
        Id = Id,
        JobId = JobId,
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
        MachineName = MachineName
    };

    /// <summary>
    /// Create entity from domain model
    /// </summary>
    public static UpscaleJobEntity FromModel(UpscaleJob job) => new()
    {
        Id = job.Id,
        JobId = job.JobId,
        SourceVideoPath = job.SourceVideoPath,
        OutputPath = job.OutputPath,
        UpscaleType = job.UpscaleType,
        SettingsJson = job.SettingsJson,
        Status = job.Status,
        ProgressPercentage = job.ProgressPercentage,
        CurrentFrame = job.CurrentFrame,
        TotalFrames = job.TotalFrames,
        EstimatedTimeRemainingTicks = job.EstimatedTimeRemaining?.Ticks,
        CreatedAt = job.CreatedAt,
        QueuedAt = job.QueuedAt,
        StartedAt = job.StartedAt,
        CompletedAt = job.CompletedAt,
        LastUpdatedAt = job.LastUpdatedAt,
        LastError = job.LastError,
        ErrorStackTrace = job.ErrorStackTrace,
        RetryCount = job.RetryCount,
        MaxRetries = job.MaxRetries,
        ProcessId = job.ProcessId,
        MachineName = job.MachineName
    };

    /// <summary>
    /// Update entity from domain model
    /// </summary>
    public void UpdateFrom(UpscaleJob job)
    {
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
        ProcessId = job.ProcessId;
        MachineName = job.MachineName;
    }
}
