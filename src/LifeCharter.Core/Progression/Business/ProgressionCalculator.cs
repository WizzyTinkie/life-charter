using LifeCharter.Core.Common.Models;
using LifeCharter.Core.Goals.Models;
using LifeCharter.Core.Journal.Business;
using LifeCharter.Core.Journal.Models;
using LifeCharter.Core.Progression.Models;
using LifeCharter.Core.Siege.Business;
using LifeCharter.Core.Siege.Models;
using LifeCharter.Core.SkillTree.Models;

namespace LifeCharter.Core.Progression.Business;

/// <summary>
/// Derives everything that is never stored: goal progress, node states, the siege, XP, level and
/// skill points. A pure function of the chart data, the date and the time zone, so every device
/// computes the same result.
/// </summary>
public static class ProgressionCalculator
{
    public static ProgressionResult Compute(LifeChartData data, DateOnly today, TimeZoneInfo zone)
    {
        var nodes = data.Nodes.Where(node => node.DeletedAt is null).ToDictionary(node => node.Id);
        var goals = data.Goals.Where(goal => goal.DeletedAt is null && nodes.ContainsKey(goal.NodeId)).ToList();
        var edges = data.Edges
            .Where(edge => edge.DeletedAt is null && nodes.ContainsKey(edge.FromNodeId) && nodes.ContainsKey(edge.ToNodeId))
            .ToList();

        var entries = JournalRules.EffectiveEntries(data.Entries).Where(entry => nodes.ContainsKey(entry.NodeId)).ToList();
        var entriesByNode = entries.ToLookup(entry => entry.NodeId);
        var goalsByNode = goals.ToLookup(goal => goal.NodeId);
        var allocatedOn = AllocationDays(data.Allocations, nodes, zone);
        var allocated = allocatedOn.Keys.ToHashSet();

        var goalProgress = goals.ToDictionary(
            goal => goal.Id,
            goal => GoalProgressCalculator.Compute(goal, entriesByNode[goal.NodeId], today, zone));

        var mastered = nodes.Values
            .Where(node => node.Kind == NodeKind.Root ||
                           (allocated.Contains(node.Id) &&
                            goalsByNode[node.Id].Any() &&
                            goalsByNode[node.Id].All(goal => goalProgress[goal.Id].IsComplete)))
            .Select(node => node.Id)
            .ToHashSet();

        var pathNeighbours = edges
            .Where(edge => edge.Kind == EdgeKind.Path)
            .SelectMany(edge => new[] { new NodeLink(edge.FromNodeId, edge.ToNodeId), new NodeLink(edge.ToNodeId, edge.FromNodeId) })
            .ToLookup(link => link.Node, link => link.Other);
        var requirements = edges
            .Where(edge => edge.Kind == EdgeKind.Requires)
            .ToLookup(edge => edge.ToNodeId, edge => edge.FromNodeId);

        var masteredOn = mastered
            .Where(id => nodes[id].Kind != NodeKind.Root)
            .ToDictionary(id => id, id => MasteryDay(goalsByNode[id].ToList(), entriesByNode[id].ToList(), allocatedOn[id], today, zone));
        var siege = SiegeEngine.Run(data, masteredOn, entries, today, zone);

        var nodeProgress = new Dictionary<Guid, NodeProgress>();
        foreach (var node in nodes.Values)
        {
            var progress = goalsByNode[node.Id].Select(goal => goalProgress[goal.Id]).ToList();
            var retention = siege.Nodes.GetValueOrDefault(node.Id);
            var state = RetentionState(ResolveState(node, allocated, mastered, pathNeighbours, requirements), retention);
            var fraction = node.Kind == NodeKind.Root ? 1 : progress.Count == 0 ? 0 : progress.Average(goal => goal.Fraction);
            nodeProgress[node.Id] = new NodeProgress(node.Id, state, fraction, progress, retention);
        }

        var xpByAttribute = Enum.GetValues<LifeAttribute>().ToDictionary(attribute => attribute, _ => 0);
        foreach (var entry in XpEarningEntries(entries, nodes, zone))
            xpByAttribute[nodes[entry.NodeId].Attribute] += ProgressionRules.EntryXp;

        // A corrupted node's mastery bonus is its "stat bonus": withheld until the node is cleansed.
        foreach (var nodeId in masteredOn.Keys.Where(id => siege.Nodes[id] is not { RewardWithheld: true }))
            xpByAttribute[nodes[nodeId].Attribute] += nodes[nodeId].XpReward;

        var totalXp = xpByAttribute.Values.Sum();
        var level = LevelCurve.LevelFor(totalXp);

        // Mastering gives the point back: points work as training slots, so finishing a node
        // always lets you pick the next one and progress can never stall.
        var training = allocated.Count(id => !mastered.Contains(id));
        var skillPoints = Math.Max(0, ProgressionRules.StartingSkillPoints + level.Level - 1 - training);

        return new ProgressionResult(new CharacterProgress(level, totalXp, skillPoints, xpByAttribute), nodeProgress, siege);
    }

    /// <summary>Replays allocations in order; a refund removes the node again. Returns the day each allocated node was (last) allocated.</summary>
    public static Dictionary<Guid, DateOnly> AllocationDays(IEnumerable<Allocation> allocations, IReadOnlyDictionary<Guid, SkillNode> nodes, TimeZoneInfo zone)
    {
        var allocated = new Dictionary<Guid, DateOnly>();
        foreach (var allocation in allocations.OrderBy(a => a.Timestamp).ThenBy(a => a.CreatedAt, StringComparer.Ordinal))
        {
            if (!nodes.TryGetValue(allocation.NodeId, out var node) || node.Kind == NodeKind.Root)
                continue;

            if (allocation.Action == AllocationAction.Allocate)
                allocated[allocation.NodeId] = GoalProgressCalculator.LocalDate(allocation.Timestamp, zone);
            else
                allocated.Remove(allocation.NodeId);
        }

        return allocated;
    }

    /// <summary>
    /// The day a mastered node's goals were all complete, but not before it was allocated. Found
    /// by binary search over the days with entries, because completion only grows with time.
    /// </summary>
    private static DateOnly MasteryDay(IReadOnlyList<Goal> goals, IReadOnlyList<LogEntry> nodeEntries, DateOnly allocatedOn, DateOnly today, TimeZoneInfo zone)
    {
        var dated = nodeEntries
            .Select(entry => new { Entry = entry, Day = GoalProgressCalculator.LocalDate(entry.Timestamp, zone) })
            .Where(item => item.Day <= today)
            .ToList();
        var days = dated.Select(item => item.Day).Distinct().Order().ToList();

        DateOnly? completedOn = null;
        var low = 0;
        var high = days.Count - 1;
        while (low <= high)
        {
            var middle = (low + high) / 2;
            var upTo = days[middle];
            var soFar = dated.Where(item => item.Day <= upTo).Select(item => item.Entry).ToList();
            if (goals.All(goal => GoalProgressCalculator.Compute(goal, soFar, upTo, zone).IsComplete))
            {
                completedOn = upTo;
                high = middle - 1;
            }
            else
            {
                low = middle + 1;
            }
        }

        return completedOn is { } completed && completed > allocatedOn ? completed : allocatedOn;
    }

    /// <summary>Refines Mastered into the retention states.</summary>
    private static NodeState RetentionState(NodeState state, NodeRetention? retention) =>
        state != NodeState.Mastered || retention is null ? state
        : retention.IsCorrupted ? NodeState.Corrupted
        : retention.Status.IsEngraved ? NodeState.Engraved
        : retention.Status.IsFortified ? NodeState.Fortified
        : NodeState.Mastered;

    private static NodeState ResolveState(
        SkillNode node,
        HashSet<Guid> allocated,
        HashSet<Guid> mastered,
        ILookup<Guid, Guid> pathNeighbours,
        ILookup<Guid, Guid> requirements)
    {
        if (node.Kind == NodeKind.Root)
            return NodeState.Mastered;

        if (allocated.Contains(node.Id))
            return mastered.Contains(node.Id) ? NodeState.Mastered : NodeState.Training;

        var required = requirements[node.Id].ToList();
        if (required.Any(id => !mastered.Contains(id)))
            return NodeState.Locked;

        return required.Count > 0 || pathNeighbours[node.Id].Any(mastered.Contains)
            ? NodeState.Available
            : NodeState.Locked;
    }

    /// <summary>Applies the daily XP cap: only the first entries per goal and day earn XP.</summary>
    private static IEnumerable<LogEntry> XpEarningEntries(IEnumerable<LogEntry> entries, IReadOnlyDictionary<Guid, SkillNode> nodes, TimeZoneInfo zone) =>
        entries
            .Where(entry => nodes.ContainsKey(entry.NodeId))
            .GroupBy(entry => new { Key = entry.GoalId ?? entry.NodeId, Date = GoalProgressCalculator.LocalDate(entry.Timestamp, zone) })
            .SelectMany(group => group.OrderBy(entry => entry.Timestamp).Take(ProgressionRules.DailyEntryCapPerGoal));

    private readonly record struct NodeLink(Guid Node, Guid Other);
}
