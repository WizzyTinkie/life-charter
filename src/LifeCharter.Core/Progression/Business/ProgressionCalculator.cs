using LifeCharter.Core.Common.Models;
using LifeCharter.Core.Journal.Models;
using LifeCharter.Core.Progression.Models;
using LifeCharter.Core.SkillTree.Models;

namespace LifeCharter.Core.Progression.Business;

/// <summary>
/// Derives everything that is never stored: goal progress, node states, XP, level and skill
/// points. A pure function of the chart data, the date and the time zone, so every device
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

        var entriesByNode = data.Entries.Where(entry => nodes.ContainsKey(entry.NodeId)).ToLookup(entry => entry.NodeId);
        var goalsByNode = goals.ToLookup(goal => goal.NodeId);
        var allocated = AllocatedNodes(data.Allocations, nodes);

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

        var nodeProgress = new Dictionary<Guid, NodeProgress>();
        foreach (var node in nodes.Values)
        {
            var progress = goalsByNode[node.Id].Select(goal => goalProgress[goal.Id]).ToList();
            var state = ResolveState(node, allocated, mastered, pathNeighbours, requirements);
            var fraction = node.Kind == NodeKind.Root ? 1 : progress.Count == 0 ? 0 : progress.Average(goal => goal.Fraction);
            nodeProgress[node.Id] = new NodeProgress(node.Id, state, fraction, progress);
        }

        var xpByAttribute = Enum.GetValues<LifeAttribute>().ToDictionary(attribute => attribute, _ => 0);
        foreach (var entry in XpEarningEntries(data.Entries, nodes, zone))
            xpByAttribute[nodes[entry.NodeId].Attribute] += ProgressionRules.EntryXp;
        foreach (var nodeId in mastered.Where(id => nodes[id].Kind != NodeKind.Root))
            xpByAttribute[nodes[nodeId].Attribute] += nodes[nodeId].XpReward;

        var totalXp = xpByAttribute.Values.Sum();
        var level = LevelCurve.LevelFor(totalXp);
        var skillPoints = Math.Max(0, ProgressionRules.StartingSkillPoints + level.Level - 1 - allocated.Count);

        return new ProgressionResult(new CharacterProgress(level, totalXp, skillPoints, xpByAttribute), nodeProgress);
    }

    /// <summary>Replays allocations in order; a refund removes the node again.</summary>
    public static HashSet<Guid> AllocatedNodes(IEnumerable<Allocation> allocations, IReadOnlyDictionary<Guid, SkillNode> nodes)
    {
        var allocated = new HashSet<Guid>();
        foreach (var allocation in allocations.OrderBy(a => a.Timestamp).ThenBy(a => a.CreatedAt, StringComparer.Ordinal))
        {
            if (!nodes.TryGetValue(allocation.NodeId, out var node) || node.Kind == NodeKind.Root)
                continue;

            if (allocation.Action == AllocationAction.Allocate)
                allocated.Add(allocation.NodeId);
            else
                allocated.Remove(allocation.NodeId);
        }

        return allocated;
    }

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
