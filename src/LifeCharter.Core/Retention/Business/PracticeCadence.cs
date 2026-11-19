using LifeCharter.Core.Goals.Models;

namespace LifeCharter.Core.Retention.Business;

/// <summary>How often a mastered node should be practised, read from the goals it was trained with.</summary>
public static class PracticeCadence
{
    /// <summary>Sessions a week assumed for goals that do not count sessions (a weekly sum of minutes).</summary>
    private const double DefaultSessionsPerWeek = 3;

    /// <summary>Days between habit repetitions: 1 for a daily goal, 7 / target for a weekly count.</summary>
    public static int HabitIntervalDays(IReadOnlyCollection<Goal> goals)
    {
        if (goals.Any(goal => goal.Window == GoalWindow.Daily) ||
            goals.FirstOrDefault(goal => goal.Window == GoalWindow.Weekly) is not { } weekly)
        {
            return 1;
        }

        return Math.Max(1, (int)Math.Floor(7 / SessionsPerWeek(weekly)));
    }

    /// <summary>
    /// Maintenance dose of a Capacity node in sessions a week: about two thirds of the training
    /// frequency, at least one (Spiering 2021: a third to two thirds less volume keeps endurance).
    /// </summary>
    public static int CapacityDose(IReadOnlyCollection<Goal> goals)
    {
        var frequency = goals.Any(goal => goal.Window == GoalWindow.Daily)
            ? 7
            : goals.FirstOrDefault(goal => goal.Window == GoalWindow.Weekly) is { } weekly
                ? SessionsPerWeek(weekly)
                : DefaultSessionsPerWeek;
        return Math.Max(1, (int)Math.Ceiling(frequency * 2 / 3));
    }

    private static double SessionsPerWeek(Goal goal) =>
        goal.Metric == GoalMetric.Count ? Math.Max(1, goal.Target) : DefaultSessionsPerWeek;
}
