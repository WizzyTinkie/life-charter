using LifeCharter.Core.Common.Interfaces;
using LifeCharter.Core.Common.Models;
using LifeCharter.Core.Goals.Models;
using LifeCharter.Core.Journal.Models;
using LifeCharter.Core.Progression.Business;
using LifeCharter.Core.Progression.Models;
using LifeCharter.Core.SkillTree.Models;

namespace LifeCharter.Core.Common.Business;

/// <summary>
/// The app's view of the chart: loads it, applies changes through the store and recomputes
/// progression after each one. UI layers read <see cref="Chart"/> and <see cref="Progression"/>
/// and refresh on <see cref="Changed"/>.
/// </summary>
public sealed class ChartSession(ILifeChartStore store, TimeProvider timeProvider)
{
    public TimeZoneInfo Zone { get; init; } = TimeZoneInfo.Local;

    public LifeChartData Chart { get; private set; } = LifeChartData.Empty;

    public ProgressionResult Progression { get; private set; } =
        ProgressionCalculator.Compute(LifeChartData.Empty, DateOnly.MinValue, TimeZoneInfo.Utc);

    public DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), Zone).DateTime);

    /// <summary>Raised after every reload or change, on the thread that made it.</summary>
    public event EventHandler? Changed;

    public async Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        Chart = await store.LoadAsync(cancellationToken).ConfigureAwait(false);
        Progression = ProgressionCalculator.Compute(Chart, Today, Zone);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public AllocationBlock CanAllocate(Guid nodeId) => AllocationRules.Check(nodeId, Chart, Progression);

    public bool CanRefund(Guid nodeId) => AllocationRules.CanRefund(nodeId, Progression);

    /// <returns><see cref="AllocationBlock.None"/> when the point was spent, otherwise why not.</returns>
    public async Task<AllocationBlock> AllocateAsync(Guid nodeId, CancellationToken cancellationToken = default)
    {
        var block = CanAllocate(nodeId);
        if (block != AllocationBlock.None)
            return block;

        await store.AddAllocationAsync(NewAllocation(nodeId, AllocationAction.Allocate), cancellationToken).ConfigureAwait(false);
        await ReloadAsync(cancellationToken).ConfigureAwait(false);
        return AllocationBlock.None;
    }

    public async Task<bool> RefundAsync(Guid nodeId, CancellationToken cancellationToken = default)
    {
        if (!CanRefund(nodeId))
            return false;

        await store.AddAllocationAsync(NewAllocation(nodeId, AllocationAction.Refund), cancellationToken).ConfigureAwait(false);
        await ReloadAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task LogAsync(Guid nodeId, Guid? goalId, double amount, string unit, string note, EntrySource source, CancellationToken cancellationToken = default)
    {
        var entry = new LogEntry
        {
            NodeId = nodeId,
            GoalId = goalId,
            Amount = amount,
            Unit = unit,
            Note = note,
            Source = source,
            Timestamp = timeProvider.GetUtcNow(),
        };
        await store.AddEntryAsync(entry, cancellationToken).ConfigureAwait(false);
        await ReloadAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Creates a node at a world position, optionally linked to an existing node, with a starter milestone goal.</summary>
    public async Task<SkillNode> CreateNodeAsync(LifeAttribute attribute, double x, double y, Guid? connectTo, CancellationToken cancellationToken = default)
    {
        var node = new SkillNode
        {
            Attribute = attribute,
            Kind = NodeKind.Small,
            Title = "New skill",
            X = Math.Round(x, 1),
            Y = Math.Round(y, 1),
            XpReward = ProgressionRules.DefaultXpReward(NodeKind.Small),
        };
        await store.SaveNodeAsync(node, cancellationToken).ConfigureAwait(false);
        await store.SaveGoalAsync(new Goal
        {
            NodeId = node.Id,
            Title = "Done",
            Metric = GoalMetric.Milestone,
            StartDate = Today,
        }, cancellationToken).ConfigureAwait(false);

        if (connectTo is { } other)
            await store.SaveEdgeAsync(new SkillEdge { FromNodeId = other, ToNodeId = node.Id, Kind = EdgeKind.Path }, cancellationToken).ConfigureAwait(false);

        await ReloadAsync(cancellationToken).ConfigureAwait(false);
        return Chart.Nodes.Single(n => n.Id == node.Id);
    }

    public async Task SaveNodeAsync(SkillNode node, CancellationToken cancellationToken = default)
    {
        await store.SaveNodeAsync(node, cancellationToken).ConfigureAwait(false);
        await ReloadAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task MoveNodeAsync(Guid nodeId, double x, double y, CancellationToken cancellationToken = default)
    {
        var node = Chart.Nodes.SingleOrDefault(n => n.Id == nodeId);
        if (node is null)
            return;

        node.X = Math.Round(x, 1);
        node.Y = Math.Round(y, 1);
        await SaveNodeAsync(node, cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteNodeAsync(Guid nodeId, CancellationToken cancellationToken = default)
    {
        if (Chart.Nodes.SingleOrDefault(n => n.Id == nodeId) is not { Kind: not NodeKind.Root })
            return;

        await store.DeleteNodeAsync(nodeId, cancellationToken).ConfigureAwait(false);
        await ReloadAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Connects two nodes with a path, or removes the path if they are already connected.</summary>
    public async Task ToggleEdgeAsync(Guid fromNodeId, Guid toNodeId, CancellationToken cancellationToken = default)
    {
        if (fromNodeId == toNodeId)
            return;

        var existing = Chart.Edges.FirstOrDefault(edge =>
            (edge.FromNodeId == fromNodeId && edge.ToNodeId == toNodeId) ||
            (edge.FromNodeId == toNodeId && edge.ToNodeId == fromNodeId));

        if (existing is not null)
            await store.DeleteEdgeAsync(existing.Id, cancellationToken).ConfigureAwait(false);
        else
            await store.SaveEdgeAsync(new SkillEdge { FromNodeId = fromNodeId, ToNodeId = toNodeId, Kind = EdgeKind.Path }, cancellationToken).ConfigureAwait(false);

        await ReloadAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task SaveGoalAsync(Goal goal, CancellationToken cancellationToken = default)
    {
        await store.SaveGoalAsync(goal, cancellationToken).ConfigureAwait(false);
        await ReloadAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteGoalAsync(Guid goalId, CancellationToken cancellationToken = default)
    {
        await store.DeleteGoalAsync(goalId, cancellationToken).ConfigureAwait(false);
        await ReloadAsync(cancellationToken).ConfigureAwait(false);
    }

    private Allocation NewAllocation(Guid nodeId, AllocationAction action) =>
        new() { NodeId = nodeId, Action = action, Timestamp = timeProvider.GetUtcNow() };
}
