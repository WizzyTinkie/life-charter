using LifeCharter.Core.Goals.Models;
using LifeCharter.Core.Progression.Business;
using LifeCharter.Core.Progression.Models;

namespace LifeCharter.Core.Tests.Progression.Business;

public sealed class GoalProgressCalculatorTests
{
    private readonly ChartBuilder chart = new();

    [Fact]
    public void Total_sum_adds_amounts_and_completes_at_target()
    {
        var reading = chart.Node("Reading");
        var goal = chart.Goal(reading, GoalMetric.Sum, 100, "pages");
        chart.Log(reading, 40, "pages", -3);
        chart.Log(reading, 70, "pages", -1);

        var progress = Compute(goal);

        Assert.Equal(110, progress.CurrentValue);
        Assert.True(progress.IsComplete);
        Assert.Equal(1, progress.Fraction);
    }

    [Fact]
    public void Daily_goal_counts_days_that_met_the_target()
    {
        var hydration = chart.Node("Hydration");
        var goal = chart.Goal(hydration, GoalMetric.Sum, 2, "l", GoalWindow.Daily, periods: 3);
        chart.Log(hydration, 2, "l", -2);
        chart.Log(hydration, 1, "l", -1);
        chart.Log(hydration, 0.5, "l", -1);
        chart.Log(hydration, 2.5, "l", 0);

        var progress = Compute(goal);

        Assert.Equal(2, progress.CompletedPeriods);
        Assert.False(progress.IsComplete);
        Assert.Equal(2.5, progress.CurrentValue);
        Assert.Equal(2 / 3.0, progress.Fraction, 6);
    }

    [Fact]
    public void Consecutive_goal_needs_an_unbroken_run()
    {
        var mind = chart.Node("Mind");
        var streak = chart.Goal(mind, GoalMetric.Count, 1, window: GoalWindow.Daily, periods: 3, consecutive: true);
        foreach (var day in new[] { -5, -4, -2, -1, 0 })
            chart.Log(mind, dayOffset: day);

        Assert.True(Compute(streak).IsComplete);
    }

    [Fact]
    public void Consecutive_goal_with_gaps_is_not_complete()
    {
        var mind = chart.Node("Mind");
        var streak = chart.Goal(mind, GoalMetric.Count, 1, window: GoalWindow.Daily, periods: 3, consecutive: true);
        foreach (var day in new[] { -4, -2, 0 })
            chart.Log(mind, dayOffset: day);

        var progress = Compute(streak);

        Assert.Equal(1, progress.CompletedPeriods);
        Assert.False(progress.IsComplete);
    }

    [Fact]
    public void Weekly_goal_groups_monday_to_sunday()
    {
        var strength = chart.Node("Strength");
        var goal = chart.Goal(strength, GoalMetric.Count, 2, "session", GoalWindow.Weekly);

        // Sunday and Monday fall into different weeks; Monday and Tuesday into the same one.
        chart.Log(strength, dayOffset: -2);
        chart.Log(strength, dayOffset: -1);
        Assert.False(Compute(goal).IsComplete);

        chart.Log(strength, dayOffset: 0);
        Assert.True(Compute(goal).IsComplete);
    }

    [Fact]
    public void Entries_before_the_start_date_do_not_count()
    {
        var steps = chart.Node("Steps");
        var goal = chart.Goal(steps, GoalMetric.Sum, 100, startOffset: -1);
        chart.Log(steps, 500, dayOffset: -3);

        Assert.Equal(0, Compute(goal).CurrentValue);
    }

    [Fact]
    public void Mismatched_units_and_other_goals_entries_are_ignored()
    {
        var cardio = chart.Node("Cardio");
        var distance = chart.Goal(cardio, GoalMetric.Sum, 10, "km");
        var minutes = chart.Goal(cardio, GoalMetric.Sum, 60, "min");
        chart.Log(cardio, 30, "min");
        chart.Log(cardio, 4, goal: minutes);
        chart.Log(cardio, 5, "KM");

        Assert.Equal(5, Compute(distance).CurrentValue);
        Assert.Equal(34, Compute(minutes).CurrentValue);
    }

    [Fact]
    public void Milestone_completes_with_one_entry()
    {
        var courses = chart.Node("Courses");
        var goal = chart.Goal(courses, GoalMetric.Milestone);
        chart.Log(courses);

        Assert.True(Compute(goal).IsComplete);
    }

    private GoalProgress Compute(Goal goal) =>
        GoalProgressCalculator.Compute(goal, chart.Build().Entries, ChartBuilder.Today, TimeZoneInfo.Utc);
}
