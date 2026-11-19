using LifeCharter.Core.Common.Models;
using LifeCharter.Core.Journal.Models;
using LifeCharter.Core.Siege.Models;
using LifeCharter.Core.SkillTree.Business;
using LifeCharter.Core.Sync.Business;
using LifeCharter.Data.Storage.Business;
using LifeCharter.Data.Sync.Business;
using LifeCharter.Data.Sync.Models;
using Microsoft.Data.Sqlite;

namespace LifeCharter.Data.Tests.Sync.Business;

/// <summary>End to end: two EF-backed devices sync through the snapshot folder.</summary>
public sealed class EfSnapshotSyncTests : IAsyncLifetime
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "lifecharter-efsync-" + Guid.NewGuid().ToString("N"));
    private Device linux = null!;
    private Device windows = null!;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        Directory.CreateDirectory(root);
        var syncFolder = Path.Combine(root, "Sync");
        linux = new Device(Path.Combine(root, "linux.db"), "linux-1", syncFolder);
        windows = new Device(Path.Combine(root, "windows.db"), "windows-1", syncFolder);

        // Only the first device starts from the template; the second gets it through sync.
        await linux.Store.InitializeAsync(StarterTemplate.Create(new DateOnly(2026, 11, 17)), Cancellation);
        await windows.Store.InitializeAsync(LifeChartData.Empty, Cancellation);
    }

    public ValueTask DisposeAsync()
    {
        SqliteConnection.ClearAllPools();
        Directory.Delete(root, recursive: true);
        return ValueTask.CompletedTask;
    }

    [Fact]
    public void Schema_covers_every_synced_table()
    {
        var tables = linux.Schema.ToDictionary(table => table.Name);

        Assert.Equal(
            ["allocations", "edges", "goals", "habit_checks", "log_entries", "nodes", "quests", "siege_settings", "vacations", "ward_uses"],
            tables.Keys.Order());
        Assert.Equal(SyncKind.AppendOnly, tables["log_entries"].Kind);
        Assert.Equal(SyncKind.AppendOnly, tables["ward_uses"].Kind);
        Assert.Equal(SyncKind.LastWriterWins, tables["nodes"].Kind);
        Assert.Equal(SyncKind.LastWriterWins, tables["siege_settings"].Kind);
        Assert.Contains("updated_at", tables["nodes"].Columns);
    }

    [Fact]
    public async Task Template_edits_and_entries_converge_across_devices()
    {
        await linux.SyncAsync();
        await windows.SyncAsync();
        var onWindows = await windows.Store.LoadAsync(Cancellation);
        Assert.Equal(17, onWindows.Nodes.Count);

        var hydration = onWindows.Nodes.Single(node => node.Title == "Hydration");
        hydration.Title = "Water";
        await windows.Store.SaveNodeAsync(hydration, Cancellation);
        await windows.Store.AddEntryAsync(new LogEntry { NodeId = hydration.Id, Amount = 2, Unit = "l", Timestamp = DateTimeOffset.UtcNow }, Cancellation);

        var steps = (await linux.Store.LoadAsync(Cancellation)).Nodes.Single(node => node.Title == "Steps");
        await linux.Store.DeleteNodeAsync(steps.Id, Cancellation);

        await windows.SyncAsync();
        await linux.SyncAsync();
        await windows.SyncAsync();

        var a = await linux.Store.LoadAsync(Cancellation);
        var b = await windows.Store.LoadAsync(Cancellation);
        Assert.Contains(a.Nodes, node => node.Title == "Water");
        Assert.DoesNotContain(b.Nodes, node => node.Title == "Steps");
        Assert.Single(a.Entries);
        Assert.Equal(a.Nodes.Select(node => node.Id).Order(), b.Nodes.Select(node => node.Id).Order());
        Assert.Equal(a.Goals.Count, b.Goals.Count);
    }

    [Fact]
    public async Task Siege_settings_from_both_devices_converge_on_the_last_write()
    {
        var day = new DateOnly(2026, 11, 18);
        await linux.Store.SaveSiegeSettingsAsync(new SiegeSettings { AttacksPerDay = 4, StartDate = day }, Cancellation);
        await windows.Store.SaveSiegeSettingsAsync(new SiegeSettings { AttacksPerDay = 9, StartDate = day }, Cancellation);
        await windows.Store.AddWardUseAsync(new WardUse { Date = day }, Cancellation);

        await linux.SyncAsync();
        await windows.SyncAsync();
        await linux.SyncAsync();

        var a = await linux.Store.LoadAsync(Cancellation);
        var b = await windows.Store.LoadAsync(Cancellation);
        Assert.Equal(9, a.SiegeSettings?.AttacksPerDay);
        Assert.Equal(9, b.SiegeSettings?.AttacksPerDay);
        Assert.Single(a.WardUses);
    }

    private sealed class Device
    {
        private readonly LifeCharterDbContextFactory factory;
        private readonly SnapshotSync sync;

        public Device(string databasePath, string deviceId, string syncFolder)
        {
            factory = new LifeCharterDbContextFactory(databasePath, new HybridLogicalClock(deviceId));
            Store = new LifeChartStore(factory);
            using var context = factory.CreateDbContext();
            Schema = SyncSchemaBuilder.FromModel(context.Model);
            sync = new SnapshotSync(syncFolder, deviceId, Schema);
        }

        public LifeChartStore Store { get; }

        public IReadOnlyList<SyncedTable> Schema { get; }

        public async Task SyncAsync()
        {
            await using var connection = new SqliteConnection(factory.ConnectionString);
            await connection.OpenAsync(Cancellation);
            var report = await sync.MergeRemoteSnapshotsAsync(connection, Cancellation);
            if (report.MaxRemoteStamp is { } stamp)
                factory.Clock.Observe(stamp);
            await sync.ExportAsync(connection, factory.Clock.Now(), Cancellation);
        }
    }
}
