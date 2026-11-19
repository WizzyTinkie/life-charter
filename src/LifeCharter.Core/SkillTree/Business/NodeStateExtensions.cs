using LifeCharter.Core.SkillTree.Models;

namespace LifeCharter.Core.SkillTree.Business;

public static class NodeStateExtensions
{
    /// <summary>The goal was reached at some point: every state of the retention phase.</summary>
    public static bool IsMastered(this NodeState state) =>
        state is NodeState.Mastered or NodeState.Fortified or NodeState.Engraved or NodeState.Corrupted;

    public static bool IsAllocated(this NodeState state) => state == NodeState.Training || state.IsMastered();
}
