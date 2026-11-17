using LifeCharter.Core.Common.Business;
using LifeCharter.Core.Common.Interfaces;
using LifeCharter.Core.Common.Models;
using LifeCharter.Core.Journal.Models;
using LifeCharter.Core.Progression.Models;
using LifeCharter.Core.SkillTree.Business;
using LifeCharter.Core.SkillTree.Models;

namespace LifeCharter.Core.Tests.Common.Business;

public sealed class ChartSessionTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 17, 18, 0, 0, TimeSpan.Zero);

    private readonly ILifeChartStore store = Substitute.For<ILifeChartStore>();
    private readonly TimeProvider time = Substitute.For<TimeProvider>();
    private readonly LifeChartData chart = StarterTemplate.Create(new DateOnly(2026, 11, 1));
    private readonly ChartSession session;

    public ChartSessionTests()
    {
        time.GetUtcNow().Returns(Now);
        store.LoadAsync(Arg.Any<CancellationToken>()).Returns(chart);
        session = new ChartSession(store, time) { Zone = TimeZoneInfo.Utc };
    }

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Reload_computes_progression_and_raises_changed()
    {
        var raised = 0;
        session.Changed += (_, _) => raised++;

        await session.ReloadAsync(Cancellation);

        Assert.Equal(1, raised);
        Assert.Equal(new DateOnly(2026, 11, 17), session.Today);
        Assert.Equal(3, session.Progression.Character.SkillPoints);
    }

    [Fact]
    public async Task Allocate_stores_an_allocation_when_allowed()
    {
        await session.ReloadAsync(Cancellation);
        var sleep = chart.Nodes.Single(node => node.Title == "Sleep");

        var block = await session.AllocateAsync(sleep.Id, Cancellation);

        Assert.Equal(AllocationBlock.None, block);
        await store.Received(1).AddAllocationAsync(
            Arg.Is<Allocation>(a => a.NodeId == sleep.Id && a.Action == AllocationAction.Allocate && a.Timestamp == Now),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Allocate_does_nothing_when_blocked()
    {
        await session.ReloadAsync(Cancellation);

        var block = await session.AllocateAsync(chart.Nodes.Single(node => node.Kind == NodeKind.Root).Id, Cancellation);

        Assert.Equal(AllocationBlock.Root, block);
        await store.DidNotReceiveWithAnyArgs().AddAllocationAsync(default!, Cancellation);
    }

    [Fact]
    public async Task Log_stores_an_entry_with_the_current_time()
    {
        await session.ReloadAsync(Cancellation);
        var water = chart.Nodes.Single(node => node.Title == "Hydration");

        await session.LogAsync(water.Id, null, 2, "l", "after run", EntrySource.Widget, Cancellation);

        await store.Received(1).AddEntryAsync(
            Arg.Is<LogEntry>(e => e.NodeId == water.Id && e.Amount == 2 && e.Unit == "l" && e.Source == EntrySource.Widget && e.Timestamp == Now),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Root_cannot_be_deleted()
    {
        await session.ReloadAsync(Cancellation);

        await session.DeleteNodeAsync(chart.Nodes.Single(node => node.Kind == NodeKind.Root).Id, Cancellation);

        await store.DidNotReceiveWithAnyArgs().DeleteNodeAsync(default, Cancellation);
    }

    [Fact]
    public async Task Toggling_an_existing_edge_deletes_it()
    {
        await session.ReloadAsync(Cancellation);
        var edge = chart.Edges[0];

        await session.ToggleEdgeAsync(edge.ToNodeId, edge.FromNodeId, Cancellation);

        await store.Received(1).DeleteEdgeAsync(edge.Id, Arg.Any<CancellationToken>());
    }
}
