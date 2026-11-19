using LifeCharter.Core.Common.Interfaces;
using LifeCharter.Core.Common.Models;
using LifeCharter.Core.Goals.Models;
using LifeCharter.Core.Journal.Business;
using LifeCharter.Core.Journal.Models;
using LifeCharter.Core.Progression.Business;
using LifeCharter.Core.Progression.Models;
using LifeCharter.Core.Retention.Models;
using LifeCharter.Core.Siege.Business;
using LifeCharter.Core.Siege.Models;
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

    /// <summary>Logs a recall session on a Memory or Motor skill node: recall first, then rate how it went.</summary>
    public async Task ReviewAsync(Guid nodeId, ReviewRating rating, EntrySource source, CancellationToken cancellationToken = default)
    {
        var entry = new LogEntry
        {
            NodeId = nodeId,
            Amount = 1,
            Unit = "recall",
            Rating = rating,
            Source = source,
            Timestamp = timeProvider.GetUtcNow(),
        };
        await store.AddEntryAsync(entry, cancellationToken).ConfigureAwait(false);
        await ReloadAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task AddHabitCheckAsync(HabitCheck check, CancellationToken cancellationToken = default)
    {
        check.Timestamp = timeProvider.GetUtcNow();
        await store.AddHabitCheckAsync(check, cancellationToken).ConfigureAwait(false);
        await ReloadAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The vacation covering today, if any.</summary>
    public Vacation? CurrentVacation =>
        Chart.Vacations.FirstOrDefault(vacation => vacation.DeletedAt is null && vacation.StartDate <= Today && Today <= vacation.EndDate);

    public bool CanUseWard =>
        Progression.Siege is { IsStarted: true, Wards: > 0, Today: { Kind: SiegeDayKind.Siege, Shielded: false } };

    /// <summary>Shields today from damage with one ward.</summary>
    public async Task<bool> UseWardAsync(CancellationToken cancellationToken = default)
    {
        if (!CanUseWard)
            return false;

        await store.AddWardUseAsync(new WardUse { Date = Today }, cancellationToken).ConfigureAwait(false);
        await ReloadAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <summary>Switches the siege on (Standard, starting today) the first time; later calls do nothing.</summary>
    public async Task StartSiegeIfNeededAsync(CancellationToken cancellationToken = default)
    {
        if (Chart.SiegeSettings is not null)
            return;

        var settings = new SiegeSettings { StartDate = Today };
        DifficultyPresets.Apply(settings, Difficulty.Standard);
        await SaveSiegeSettingsAsync(settings, cancellationToken).ConfigureAwait(false);
    }

    public async Task SaveSiegeSettingsAsync(SiegeSettings settings, CancellationToken cancellationToken = default)
    {
        await store.SaveSiegeSettingsAsync(settings, cancellationToken).ConfigureAwait(false);
        await ReloadAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Pauses the siege from <paramref name="start"/> to <paramref name="end"/>, both included.</summary>
    public async Task StartVacationAsync(DateOnly start, DateOnly end, CancellationToken cancellationToken = default)
    {
        if (end < start)
            (start, end) = (end, start);

        await store.SaveVacationAsync(new Vacation { StartDate = start, EndDate = end }, cancellationToken).ConfigureAwait(false);
        await ReloadAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Ends a vacation now: today is a normal siege day again. A vacation that has not begun is removed.</summary>
    public async Task EndVacationAsync(Guid vacationId, CancellationToken cancellationToken = default)
    {
        var vacation = Chart.Vacations.FirstOrDefault(v => v.Id == vacationId && v.DeletedAt is null);
        if (vacation is null)
            return;

        if (vacation.StartDate >= Today)
        {
            await store.DeleteVacationAsync(vacation.Id, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            vacation.EndDate = Today.AddDays(-1);
            await store.SaveVacationAsync(vacation, cancellationToken).ConfigureAwait(false);
        }

        await ReloadAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Entries that still count, newest first; optionally only those of one node.</summary>
    public IReadOnlyList<LogEntry> RecentEntries(Guid? nodeId, int count) =>
        JournalRules.EffectiveEntries(Chart.Entries)
            .Where(entry => nodeId is null || entry.NodeId == nodeId)
            .OrderByDescending(entry => entry.Timestamp)
            .Take(count)
            .ToList();

    /// <summary>Cancels an entry by adding a retraction; the original stays in the history.</summary>
    public async Task<bool> UndoEntryAsync(Guid entryId, CancellationToken cancellationToken = default)
    {
        var entry = JournalRules.EffectiveEntries(Chart.Entries).FirstOrDefault(e => e.Id == entryId);
        if (entry is null)
            return false;

        await store.AddEntryAsync(new LogEntry
        {
            NodeId = entry.NodeId,
            GoalId = entry.GoalId,
            Amount = 0,
            Unit = entry.Unit,
            Note = "undo",
            Source = entry.Source,
            Timestamp = timeProvider.GetUtcNow(),
            RetractsEntryId = entry.Id,
        }, cancellationToken).ConfigureAwait(false);
        await ReloadAsync(cancellationToken).ConfigureAwait(false);
        return true;
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
