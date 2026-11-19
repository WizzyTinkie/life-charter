using LifeCharter.Core.SkillTree.Business;
using LifeCharter.Core.Sync.Business;
using LifeCharter.Data.Storage.Business;
using Microsoft.Data.Sqlite;

namespace LifeCharter.Data.Tests.Storage.Business;

public sealed class DatabaseBackupTests : IAsyncLifetime
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "lifecharter-backup-" + Guid.NewGuid().ToString("N"));
    private LifeCharterDbContextFactory factory = null!;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private string BackupFolder => Path.Combine(root, "backups");

    public async ValueTask InitializeAsync()
    {
        Directory.CreateDirectory(root);
        factory = new LifeCharterDbContextFactory(Path.Combine(root, "lifecharter.db"), new HybridLogicalClock("test"));
        await new LifeChartStore(factory).InitializeAsync(StarterTemplate.Create(new DateOnly(2026, 11, 19)), Cancellation);
    }

    public ValueTask DisposeAsync()
    {
        SqliteConnection.ClearAllPools();
        Directory.Delete(root, recursive: true);
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task One_backup_a_day()
    {
        var day = new DateOnly(2026, 11, 19);

        Assert.NotNull(await DatabaseBackup.CreateDailyAsync(factory.ConnectionString, BackupFolder, day, Cancellation));
        Assert.Null(await DatabaseBackup.CreateDailyAsync(factory.ConnectionString, BackupFolder, day, Cancellation));
        Assert.Single(Directory.GetFiles(BackupFolder));
    }

    [Fact]
    public async Task Only_the_newest_fourteen_are_kept()
    {
        var first = new DateOnly(2026, 11, 1);
        for (var i = 0; i < 16; i++)
            await DatabaseBackup.CreateDailyAsync(factory.ConnectionString, BackupFolder, first.AddDays(i), Cancellation);

        var names = Directory.GetFiles(BackupFolder).Select(Path.GetFileName).Order().ToList();
        Assert.Equal(DatabaseBackup.Keep, names.Count);
        Assert.Equal("lifecharter-2026-11-03.db", names[0]);
    }
}
