using LifeCharter.Core.Goals.Models;
using LifeCharter.Core.Progression.Models;

namespace LifeCharter.App.SkillTree.Business;

/// <summary>Short human-readable texts for progression values.</summary>
public static class ProgressText
{
    public static string Goal(Goal goal, GoalProgress progress)
    {
        if (goal.Metric == GoalMetric.Milestone)
            return progress.IsComplete ? "Done" : "Not done yet";

        var unit = string.IsNullOrWhiteSpace(goal.Unit) ? "" : " " + goal.Unit;
        return goal.Window switch
        {
            GoalWindow.Daily => $"{progress.CompletedPeriods}/{progress.RequiredPeriods} days · today {progress.CurrentValue:0.##}/{goal.Target:0.##}{unit}",
            GoalWindow.Weekly => $"{progress.CompletedPeriods}/{progress.RequiredPeriods} weeks · this week {progress.CurrentValue:0.##}/{goal.Target:0.##}{unit}",
            _ => $"{progress.CurrentValue:0.##}/{goal.Target:0.##}{unit}",
        };
    }

    public static string Block(AllocationBlock block) => block switch
    {
        AllocationBlock.None => "",
        AllocationBlock.Root => "The root is always yours.",
        AllocationBlock.AlreadyAllocated => "Already allocated.",
        AllocationBlock.NoGoal => "Add a goal first.",
        AllocationBlock.NoSkillPoints => "No skill points left. Level up to earn more.",
        AllocationBlock.NotConnected => "Master a neighbouring node first.",
        AllocationBlock.RequirementsUnmet => "Master its prerequisites first.",
        AllocationBlock.WorkInProgressLimit => "3 nodes of this attribute are already in training. Finish or refund one.",
        _ => block.ToString(),
    };

    /// <summary>A sensible amount to pre-fill when logging towards a goal.</summary>
    public static double DefaultAmount(Goal goal) =>
        goal.Metric == GoalMetric.Sum && goal.Window == GoalWindow.Daily ? goal.Target : 1;
}
