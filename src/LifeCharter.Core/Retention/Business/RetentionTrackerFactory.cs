using LifeCharter.Core.Goals.Models;
using LifeCharter.Core.Retention.Interfaces;
using LifeCharter.Core.SkillTree.Models;

namespace LifeCharter.Core.Retention.Business;

internal static class RetentionTrackerFactory
{
    public static IRetentionTracker Create(SkillNode node, IReadOnlyCollection<Goal> goals, DateOnly masteredOn, double desiredRetention) =>
        node.RetentionModel switch
        {
            RetentionModel.Memory => new MemoryTracker(masteredOn, desiredRetention),
            RetentionModel.Habit => new HabitTracker(PracticeCadence.HabitIntervalDays(goals), node.Complexity),
            RetentionModel.Capacity => new CapacityTracker(masteredOn, PracticeCadence.CapacityDose(goals)),
            RetentionModel.MotorSkill => new MotorSkillTracker(masteredOn, desiredRetention),
            _ => new MilestoneTracker(),
        };
}
