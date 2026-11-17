using LifeCharter.Core.Sync.Business;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace LifeCharter.Data.Storage.Business;

/// <summary>Creates contexts for one device's working database, all sharing that device's clock.</summary>
public sealed class LifeCharterDbContextFactory(string databasePath, HybridLogicalClock clock) : IDbContextFactory<LifeCharterDbContext>
{
    public string ConnectionString { get; } = new SqliteConnectionStringBuilder { DataSource = databasePath }.ToString();

    public HybridLogicalClock Clock => clock;

    public LifeCharterDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<LifeCharterDbContext>().UseSqlite(ConnectionString).Options, clock);
}
