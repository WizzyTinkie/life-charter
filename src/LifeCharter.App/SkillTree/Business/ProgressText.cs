using System.Globalization;
using LifeCharter.Core.Goals.Models;
using LifeCharter.Core.Journal.Models;
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

    /// <summary>"2 l · today 14:02 · after run"</summary>
    public static string Entry(LogEntry entry, TimeZoneInfo zone, DateOnly today)
    {
        var local = TimeZoneInfo.ConvertTime(entry.Timestamp, zone);
        var date = DateOnly.FromDateTime(local.DateTime);
        var day = date == today ? "today"
            : date == today.AddDays(-1) ? "yesterday"
            : local.ToString("ddd d MMM", CultureInfo.CurrentCulture);
        var amount = entry.Rating is { } rating ? $"recall · {rating}"
            : string.IsNullOrWhiteSpace(entry.Unit) ? $"{entry.Amount:0.##}×"
            : $"{entry.Amount:0.##} {entry.Unit}";
        var note = string.IsNullOrWhiteSpace(entry.Note) ? "" : $" · {entry.Note}";
        return $"{amount} · {day} {local:HH:mm}{note}";
    }
}
