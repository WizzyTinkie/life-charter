using LifeCharter.Core.SkillTree.Models;

namespace LifeCharter.Core.Progression.Models;

public sealed record NodeProgress(Guid NodeId, NodeState State, double Fraction, IReadOnlyList<GoalProgress> Goals);
