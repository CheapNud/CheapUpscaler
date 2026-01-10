using CheapUpscaler.Blazor.Models;
using Microsoft.EntityFrameworkCore;

namespace CheapUpscaler.Blazor.Data;

/// <summary>
/// Repository interface for upscale job persistence
/// </summary>
public interface IUpscaleJobRepository
{
    Task<UpscaleJob?> GetByIdAsync(int id);
    Task<UpscaleJob?> GetByJobIdAsync(Guid jobId);
    Task<IEnumerable<UpscaleJob>> GetAllAsync();
    Task<IEnumerable<UpscaleJob>> GetByStatusAsync(params UpscaleJobStatus[] statuses);
    Task<UpscaleJob> AddAsync(UpscaleJob job);
    Task UpdateAsync(UpscaleJob job);
    Task DeleteAsync(Guid jobId);
    Task<int> DeleteByStatusAsync(params UpscaleJobStatus[] statuses);
    Task<int> GetCountByStatusAsync(UpscaleJobStatus status);
}

/// <summary>
/// EF Core implementation of upscale job repository
/// </summary>
public class UpscaleJobRepository(IDbContextFactory<UpscaleJobDbContext> contextFactory) : IUpscaleJobRepository
{
    public async Task<UpscaleJob?> GetByIdAsync(int id)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        var entity = await context.Jobs.FindAsync(id);
        return entity?.ToModel();
    }

    public async Task<UpscaleJob?> GetByJobIdAsync(Guid jobId)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        var entity = await context.Jobs.FirstOrDefaultAsync(j => j.JobId == jobId);
        return entity?.ToModel();
    }

    public async Task<IEnumerable<UpscaleJob>> GetAllAsync()
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        var entities = await context.Jobs
            .OrderByDescending(j => j.CreatedAt)
            .ToListAsync();
        return entities.Select(e => e.ToModel());
    }

    public async Task<IEnumerable<UpscaleJob>> GetByStatusAsync(params UpscaleJobStatus[] statuses)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        var entities = await context.Jobs
            .Where(j => statuses.Contains(j.Status))
            .OrderByDescending(j => j.CreatedAt)
            .ToListAsync();
        return entities.Select(e => e.ToModel());
    }

    public async Task<UpscaleJob> AddAsync(UpscaleJob job)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        var entity = UpscaleJobEntity.FromModel(job);
        context.Jobs.Add(entity);
        await context.SaveChangesAsync();
        job.Id = entity.Id;
        return job;
    }

    public async Task UpdateAsync(UpscaleJob job)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        var entity = await context.Jobs.FirstOrDefaultAsync(j => j.JobId == job.JobId);
        if (entity != null)
        {
            entity.UpdateFrom(job);
            await context.SaveChangesAsync();
        }
    }

    public async Task DeleteAsync(Guid jobId)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        var entity = await context.Jobs.FirstOrDefaultAsync(j => j.JobId == jobId);
        if (entity != null)
        {
            context.Jobs.Remove(entity);
            await context.SaveChangesAsync();
        }
    }

    public async Task<int> DeleteByStatusAsync(params UpscaleJobStatus[] statuses)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        var entities = await context.Jobs
            .Where(j => statuses.Contains(j.Status))
            .ToListAsync();
        context.Jobs.RemoveRange(entities);
        await context.SaveChangesAsync();
        return entities.Count;
    }

    public async Task<int> GetCountByStatusAsync(UpscaleJobStatus status)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        return await context.Jobs.CountAsync(j => j.Status == status);
    }
}
