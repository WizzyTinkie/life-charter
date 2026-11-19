using LifeCharter.Core.Goals.Models;
using LifeCharter.Core.Retention.Business;
using LifeCharter.Core.Retention.Models;
using LifeCharter.Core.SkillTree.Models;

namespace LifeCharter.Core.Tests.Retention.Business;

public sealed class RetentionTrackerTests
{
    private static readonly DateOnly MasteredOn = new(2026, 1, 4);

    [Fact]
    public void Automaticity_reaches_95_percent_at_the_plateau() =>
        Assert.Equal(0.95, RetentionRules.Automaticity(66, 66), 6);

    [Fact]
    public void Memory_node_is_due_the_day_after_mastery()
    {
        var tracker = new MemoryTracker(MasteredOn, 0.9);
        tracker.Advance(MasteredOn, DayPractice.None, paused: false);

        Assert.True(tracker.IsDue(MasteredOn.AddDays(1)));
    }

    [Fact]
    public void Easy_reviews_need_five_reviews_over_half_a_year_to_engrave()
    {
        var tracker = new MemoryTracker(MasteredOn, 0.9);
        var day = MasteredOn;
        tracker.Advance(day, DayPractice.None, paused: false);

        var reviews = 0;
        var stableOn = (DateOnly?)null;
        while (!tracker.Status(day).IsEngraved && reviews < 30)
        {
            day = day.AddDays(1);
            while (!tracker.IsDue(day))
                day = day.AddDays(1);

            tracker.Advance(day, new DayPractice(0, [ReviewRating.Easy], []), paused: false);
            reviews++;
            if (stableOn is null && tracker.Status(day).Criteria[0].IsMet)
                stableOn = day;
        }

        Assert.True(tracker.Status(day).IsEngraved);
        Assert.True(stableOn < day, "Stability passes 365 days before the guard allows engraving.");
        Assert.True(reviews >= RetentionRules.EngravedMinimumReviews);
    }

    [Fact]
    public void Memory_estimate_lies_in_the_future()
    {
        var tracker = new MemoryTracker(MasteredOn, 0.9);
        tracker.Advance(MasteredOn, DayPractice.None, paused: false);

        var estimate = tracker.Status(MasteredOn).EstimatedDate;

        Assert.NotNull(estimate);
        Assert.InRange(estimate.Value.DayNumber - MasteredOn.DayNumber, 182, 730);
    }

    [Theory]
    [InlineData(Complexity.Simple, 33)]
    [InlineData(Complexity.Standard, 66)]
    [InlineData(Complexity.Complex, 132)]
    public void Habit_asks_for_a_self_check_after_the_plateau_repetitions(Complexity complexity, int repetitions)
    {
        var tracker = new HabitTracker(1, complexity);
        var day = MasteredOn;
        for (var i = 0; i < repetitions - 1; i++, day = day.AddDays(1))
            tracker.Advance(day, new DayPractice(1, [], []), paused: false);

        Assert.False(tracker.Status(day).NeedsSelfCheck);

        tracker.Advance(day, new DayPractice(1, [], []), paused: false);
        Assert.True(tracker.Status(day).NeedsSelfCheck);
        Assert.False(tracker.Status(day).IsEngraved);
    }

    [Fact]
    public void Habit_engraves_only_with_a_passed_self_check_taken_when_ready()
    {
        var tracker = new HabitTracker(1, Complexity.Simple);
        var day = MasteredOn;
        tracker.Advance(day, new DayPractice(1, [], [Check(5)]), paused: false);
        for (var i = 1; i < 33; i++)
        {
            day = day.AddDays(1);
            tracker.Advance(day, new DayPractice(1, [], []), paused: false);
        }

        Assert.False(tracker.Status(day).IsEngraved);

        day = day.AddDays(1);
        tracker.Advance(day, new DayPractice(1, [], [Check(3)]), paused: false);
        Assert.False(tracker.Status(day).IsEngraved);

        day = day.AddDays(1);
        tracker.Advance(day, new DayPractice(1, [], [Check(4)]), paused: false);
        Assert.True(tracker.Status(day).IsEngraved);
        Assert.False(tracker.IsDue(day.AddDays(5)));
    }

    [Fact]
    public void Each_habit_miss_costs_one_repetition_and_raises_severity()
    {
        var tracker = new HabitTracker(1, Complexity.Standard);
        tracker.Advance(MasteredOn, new DayPractice(1, [], []), paused: false);
        tracker.Advance(MasteredOn.AddDays(1), DayPractice.None, paused: false);
        tracker.Miss(MasteredOn.AddDays(1));
        tracker.Advance(MasteredOn.AddDays(2), DayPractice.None, paused: false);
        tracker.Miss(MasteredOn.AddDays(2));

        Assert.Equal(0, tracker.Status(MasteredOn.AddDays(2)).Criteria[0].Current);
        Assert.Equal(1.2, tracker.Severity(MasteredOn.AddDays(3)), 6);
    }

    [Fact]
    public void Capacity_is_harmless_within_the_grace_period()
    {
        var tracker = new CapacityTracker(MasteredOn, dose: 2);
        tracker.Advance(MasteredOn, new DayPractice(1, [], []), paused: false);
        var day = MasteredOn;
        for (var i = 0; i < 13; i++)
        {
            day = day.AddDays(1);
            tracker.Advance(day, DayPractice.None, paused: false);
        }

        Assert.True(tracker.IsDue(day.AddDays(1)));
        Assert.Equal(0, tracker.Severity(day.AddDays(1)));

        day = day.AddDays(1);
        tracker.Advance(day, DayPractice.None, paused: false);
        Assert.Equal(1, tracker.Severity(day.AddDays(1)));
    }

    [Fact]
    public void Vacation_days_do_not_count_towards_the_capacity_gap()
    {
        var tracker = new CapacityTracker(MasteredOn, dose: 2);
        tracker.Advance(MasteredOn, new DayPractice(1, [], []), paused: false);
        for (var i = 1; i <= 20; i++)
            tracker.Advance(MasteredOn.AddDays(i), DayPractice.None, paused: true);

        Assert.False(tracker.IsDue(MasteredOn.AddDays(21)));
    }

    [Fact]
    public void Motor_skill_engraves_after_four_spaced_sessions_over_three_weeks()
    {
        var tracker = new MotorSkillTracker(MasteredOn, 0.9);
        foreach (var offset in new[] { 2, 3, 9, 16, 23 })
            tracker.Advance(MasteredOn.AddDays(offset), new DayPractice(1, [], []), paused: false);

        var status = tracker.Status(MasteredOn.AddDays(23));
        Assert.True(status.IsEngraved);
        Assert.Equal(4, status.Criteria[0].Current);
    }

    [Fact]
    public void Practice_cadence_comes_from_the_goals()
    {
        Goal Weekly(GoalMetric metric, double target) => new() { Window = GoalWindow.Weekly, Metric = metric, Target = target };

        Assert.Equal(1, PracticeCadence.HabitIntervalDays([new Goal { Window = GoalWindow.Daily }]));
        Assert.Equal(2, PracticeCadence.HabitIntervalDays([Weekly(GoalMetric.Count, 3)]));
        Assert.Equal(7, PracticeCadence.HabitIntervalDays([Weekly(GoalMetric.Count, 1)]));
        Assert.Equal(2, PracticeCadence.CapacityDose([Weekly(GoalMetric.Sum, 60)]));
        Assert.Equal(5, PracticeCadence.CapacityDose([new Goal { Window = GoalWindow.Daily }]));
        Assert.Equal(1, PracticeCadence.CapacityDose([Weekly(GoalMetric.Count, 1)]));
    }

    private static HabitCheck Check(int answer) =>
        new() { Automatically = answer, WithoutRemembering = answer, WithoutThinking = answer, BeforeRealizing = answer };
}
