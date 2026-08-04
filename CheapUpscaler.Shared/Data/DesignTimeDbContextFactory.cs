using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CheapUpscaler.Shared.Data;

/// <summary>
/// Design-time factory for `dotnet ef migrations` — never used at runtime.
/// A throwaway connection string is fine; migrations only need the model.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<UpscaleJobDbContext>
{
    public UpscaleJobDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<UpscaleJobDbContext>();
        optionsBuilder.UseSqlite("Data Source=design-time.db");
        return new UpscaleJobDbContext(optionsBuilder.Options);
    }
}
