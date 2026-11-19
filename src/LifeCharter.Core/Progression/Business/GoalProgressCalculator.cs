using LifeCharter.Core.Goals.Models;
using LifeCharter.Core.Journal.Models;
using LifeCharter.Core.Progression.Models;

namespace LifeCharter.Core.Progression.Business;

public static class GoalProgressCalculator
{
    public static GoalProgress Compute(Goal goal, IEnumerable<LogEntry> nodeEntries, DateOnly today, TimeZoneInfo zone)
    {
        var entries = nodeEntries
            .Where(entry => CountsFor(goal, entry))
            .Select(entry => new { Date = LocalDate(entry.Timestamp, zone), Value = goal.Metric == GoalMetric.Sum ? entry.Amount : 1 })
            .Where(entry => entry.Date >= goal.StartDate)
            .ToList();

        if (goal.Metric == GoalMetric.Milestone || goal.Window == GoalWindow.Total)
        {
            var target = goal.Metric == GoalMetric.Milestone ? 1 : goal.Target;
            var total = entries.Sum(entry => entry.Value);
            var done = total >= target;
            return new GoalProgress(goal.Id, total, target, done ? 1 : 0, 1, Fraction(total, target), done);
        }

        var perPeriod = entries
            .GroupBy(entry => PeriodStart(entry.Date, goal.Window))
            .ToDictionary(group => group.Key, group => group.Sum(entry => entry.Value));

        var metPeriods = perPeriod.Where(pair => pair.Value >= goal.Target).Select(pair => pair.Key).Order().ToList();
        var required = Math.Max(1, goal.Periods);
        var achieved = goal.Consecutive ? LongestRun(metPeriods, goal.Window) : metPeriods.Count;
        var current = perPeriod.GetValueOrDefault(PeriodStart(today, goal.Window));

        return new GoalProgress(goal.Id, current, goal.Target, achieved, required, Fraction(achieved, required), achieved >= required);
    }

    /// <summary>
    /// An entry with a goal counts only for that goal. Without one it counts for every goal of
    /// its node whose unit matches (an empty unit on either side matches anything). Recall
    /// sessions (rated entries) never count for goals.
    /// </summary>
    public static bool CountsFor(Goal goal, LogEntry entry) =>
        entry.Rating is null &&
        (entry.GoalId is { } goalId
            ? goalId == goal.Id
            : entry.NodeId == goal.NodeId &&
              (string.IsNullOrWhiteSpace(goal.Unit) ||
               string.IsNullOrWhiteSpace(entry.Unit) ||
               string.Equals(goal.Unit.Trim(), entry.Unit.Trim(), StringComparison.OrdinalIgnoreCase)));

    public static DateOnly LocalDate(DateTimeOffset timestamp, TimeZoneInfo zone) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(timestamp, zone).DateTime);

    private static DateOnly PeriodStart(DateOnly date, GoalWindow window) =>
        window == GoalWindow.Weekly ? date.AddDays(-(((int)date.DayOfWeek + 6) % 7)) : date;

    private static int LongestRun(List<DateOnly> sortedPeriodStarts, GoalWindow window)
    {
        var step = window == GoalWindow.Weekly ? 7 : 1;
        var best = 0;
        var run = 0;
        DateOnly? previous = null;

        foreach (var start in sortedPeriodStarts)
        {
            run = previous is { } p && start.DayNumber - p.DayNumber == step ? run + 1 : 1;
            best = Math.Max(best, run);
            previous = start;
        }

        return best;
    }

    private static double Fraction(double value, double target) =>
        target <= 0 ? 1 : Math.Clamp(value / target, 0, 1);
}
