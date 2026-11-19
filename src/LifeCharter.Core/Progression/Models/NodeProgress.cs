using LifeCharter.Core.Siege.Models;
using LifeCharter.Core.SkillTree.Models;

namespace LifeCharter.Core.Progression.Models;

/// <summary>Derived progress of one node. <see cref="Retention"/> is set once the node is mastered.</summary>
public sealed record NodeProgress(
    Guid NodeId,
    NodeState State,
    double Fraction,
    IReadOnlyList<GoalProgress> Goals,
    NodeRetention? Retention = null);
