using LifeCharter.Core.Sync.Business;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace LifeCharter.Data.Storage.Business;

/// <summary>Used only by <c>dotnet ef</c> to create migrations.</summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<LifeCharterDbContext>
{
    public LifeCharterDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<LifeCharterDbContext>().UseSqlite("Data Source=design-time.db").Options,
            new HybridLogicalClock("design-time"));
}
