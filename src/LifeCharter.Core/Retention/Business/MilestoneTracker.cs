using LifeCharter.Core.Retention.Interfaces;
using LifeCharter.Core.Retention.Models;
using LifeCharter.Core.SkillTree.Models;

namespace LifeCharter.Core.Retention.Business;

/// <summary>A one-off achievement: it happened, so it is engraved the moment it is mastered.</summary>
internal sealed class MilestoneTracker : IRetentionTracker
{
    public bool IsFortified => true;

    public bool IsDue(DateOnly day) => false;

    public double Severity(DateOnly day) => 0;

    public bool Defends(DayPractice practice) => false;

    public void Advance(DateOnly day, DayPractice practice, bool paused)
    {
    }

    public void Miss(DateOnly day)
    {
    }

    public RetentionStatus Status(DateOnly today) =>
        new(RetentionModel.Milestone, IsFortified: true, IsEngraved: true, Fraction: 1, Criteria: [], EstimatedDate: null);
}
