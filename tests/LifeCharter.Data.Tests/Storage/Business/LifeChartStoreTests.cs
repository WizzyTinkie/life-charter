using LifeCharter.Core.Common.Models;
using LifeCharter.Core.Journal.Models;
using LifeCharter.Core.Progression.Models;
using LifeCharter.Core.SkillTree.Business;
using LifeCharter.Core.SkillTree.Models;
using LifeCharter.Core.Sync.Business;
using LifeCharter.Data.Storage.Business;
using Microsoft.Data.Sqlite;

namespace LifeCharter.Data.Tests.Storage.Business;

public sealed class LifeChartStoreTests : IAsyncLifetime
{
    private const string DeviceId = "test-device";

    private readonly string root = Path.Combine(Path.GetTempPath(), "lifecharter-store-" + Guid.NewGuid().ToString("N"));
    private LifeCharterDbContextFactory factory = null!;
    private LifeChartStore store = null!;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        Directory.CreateDirectory(root);
        factory = new LifeCharterDbContextFactory(Path.Combine(root, "lifecharter.db"), new HybridLogicalClock(DeviceId));
        store = new LifeChartStore(factory);
        await store.InitializeAsync(StarterTemplate.Create(new DateOnly(2026, 11, 17)), Cancellation);
    }

    public ValueTask DisposeAsync()
    {
        SqliteConnection.ClearAllPools();
        Directory.Delete(root, recursive: true);
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task Empty_database_is_seeded_once()
    {
        await store.InitializeAsync(StarterTemplate.Create(new DateOnly(2026, 11, 18)), Cancellation);

        var chart = await store.LoadAsync(Cancellation);
        Assert.Equal(17, chart.Nodes.Count);
        Assert.Equal(16, chart.Goals.Count);
        Assert.Equal(16, chart.Edges.Count);
    }

    [Fact]
    public async Task Saved_rows_are_stamped_with_device_and_clock()
    {
        var chart = await store.LoadAsync(Cancellation);

        Assert.All(chart.Nodes, node =>
        {
            Assert.Equal(DeviceId, node.DeviceId);
            Assert.EndsWith("-" + DeviceId, node.UpdatedAt);
        });
    }

    [Fact]
    public async Task Entries_and_allocations_round_trip()
    {
        var sleep = (await store.LoadAsync(Cancellation)).Nodes.Single(node => node.Title == "Sleep");
        await store.AddAllocationAsync(new Allocation { NodeId = sleep.Id, Action = AllocationAction.Allocate, Timestamp = DateTimeOffset.UtcNow }, Cancellation);
        await store.AddEntryAsync(new LogEntry { NodeId = sleep.Id, Amount = 7.5, Unit = "h", Timestamp = DateTimeOffset.UtcNow, Note = "good night" }, Cancellation);

        var chart = await store.LoadAsync(Cancellation);
        var entry = Assert.Single(chart.Entries);
        Assert.Equal(7.5, entry.Amount);
        Assert.Equal("good night", entry.Note);
        Assert.NotEmpty(entry.CreatedAt);
        Assert.Equal(AllocationAction.Allocate, Assert.Single(chart.Allocations).Action);
    }

    [Fact]
    public async Task Editing_a_node_bumps_its_updated_at()
    {
        var node = (await store.LoadAsync(Cancellation)).Nodes.Single(n => n.Title == "Reading");
        var before = node.UpdatedAt;

        node.Title = "Reading books";
        await store.SaveNodeAsync(node, Cancellation);

        var saved = (await store.LoadAsync(Cancellation)).Nodes.Single(n => n.Id == node.Id);
        Assert.Equal("Reading books", saved.Title);
        Assert.True(string.CompareOrdinal(saved.UpdatedAt, before) > 0);
    }

    [Fact]
    public async Task Deleting_a_node_tombstones_it_with_its_edges_and_goals()
    {
        var chart = await store.LoadAsync(Cancellation);
        var steps = chart.Nodes.Single(node => node.Title == "Steps");

        await store.DeleteNodeAsync(steps.Id, Cancellation);

        var after = await store.LoadAsync(Cancellation);
        Assert.DoesNotContain(after.Nodes, node => node.Id == steps.Id);
        Assert.DoesNotContain(after.Goals, goal => goal.NodeId == steps.Id);
        Assert.DoesNotContain(after.Edges, edge => edge.ToNodeId == steps.Id);

        await using var connection = new SqliteConnection(factory.ConnectionString);
        await connection.OpenAsync(Cancellation);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT deleted_at FROM nodes WHERE id = $id";
        cmd.Parameters.AddWithValue("$id", steps.Id.ToString().ToUpperInvariant());
        Assert.IsType<string>(await cmd.ExecuteScalarAsync(Cancellation));
    }

    [Fact]
    public async Task Append_only_rows_cannot_be_changed()
    {
        var node = (await store.LoadAsync(Cancellation)).Nodes.First(n => n.Kind != NodeKind.Root);
        var entry = new LogEntry { NodeId = node.Id, Timestamp = DateTimeOffset.UtcNow };
        await store.AddEntryAsync(entry, Cancellation);

        await using var context = factory.CreateDbContext();
        context.Entries.Attach(entry);
        entry.Amount = 99;
        context.Entries.Update(entry);

        await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync(Cancellation));
    }
}
