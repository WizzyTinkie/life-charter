using LifeCharter.Core.Common.Models;
using LifeCharter.Core.Progression.Models;
using LifeCharter.Core.SkillTree.Models;

namespace LifeCharter.Core.Progression.Business;

public static class AllocationRules
{
    public static AllocationBlock Check(Guid nodeId, LifeChartData data, ProgressionResult progression)
    {
        var nodes = data.Nodes.Where(node => node.DeletedAt is null).ToDictionary(node => node.Id);
        if (!nodes.TryGetValue(nodeId, out var node))
            throw new ArgumentException($"Node {nodeId} does not exist.", nameof(nodeId));

        if (node.Kind == NodeKind.Root)
            return AllocationBlock.Root;

        var state = progression.Nodes[nodeId].State;
        if (state is NodeState.Training or NodeState.Mastered)
            return AllocationBlock.AlreadyAllocated;

        if (!data.Goals.Any(goal => goal.NodeId == nodeId && goal.DeletedAt is null))
            return AllocationBlock.NoGoal;

        if (state == NodeState.Locked)
        {
            var requirementUnmet = data.Edges.Any(edge =>
                edge.DeletedAt is null &&
                edge.Kind == EdgeKind.Requires &&
                edge.ToNodeId == nodeId &&
                progression.Nodes.TryGetValue(edge.FromNodeId, out var from) &&
                from.State != NodeState.Mastered);
            return requirementUnmet ? AllocationBlock.RequirementsUnmet : AllocationBlock.NotConnected;
        }

        if (progression.Character.SkillPoints <= 0)
            return AllocationBlock.NoSkillPoints;

        var training = progression.Nodes.Values.Count(progress =>
            progress.State == NodeState.Training &&
            nodes.TryGetValue(progress.NodeId, out var other) &&
            other.Attribute == node.Attribute);

        return training >= ProgressionRules.WorkInProgressLimit ? AllocationBlock.WorkInProgressLimit : AllocationBlock.None;
    }

    /// <summary>Abandoning a node in training refunds its point. Mastered nodes stay mastered (their point is already back).</summary>
    public static bool CanRefund(Guid nodeId, ProgressionResult progression) =>
        progression.Nodes.TryGetValue(nodeId, out var progress) && progress.State == NodeState.Training;
}
