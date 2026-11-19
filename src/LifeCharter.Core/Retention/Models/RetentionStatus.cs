using LifeCharter.Core.SkillTree.Models;

namespace LifeCharter.Core.Retention.Models;

/// <summary>
/// Where a mastered node stands on its way to being learned for good (PLAN.md section 4).
/// <see cref="Fraction"/> is the Engrave progress; for Capacity nodes, which never engrave, it is
/// the progress towards Seasoned.
/// </summary>
public sealed record RetentionStatus(
    RetentionModel Model,
    bool IsFortified,
    bool IsEngraved,
    double Fraction,
    IReadOnlyList<EngraveCriterion> Criteria,
    DateOnly? EstimatedDate,
    bool NeedsSelfCheck = false)
{
    public bool CanEngrave => Model != RetentionModel.Capacity;
}
