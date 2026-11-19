using LifeCharter.Core.Retention.Interfaces;
using LifeCharter.Core.Retention.Models;
using LifeCharter.Core.SkillTree.Models;

namespace LifeCharter.Core.Retention.Business;

/// <summary>
/// Fitness needs a minimal maintenance dose and fades after about two weeks without it
/// (Spiering 2021). The node is due when the next session of the dose is expected; attacks deal
/// damage only after the grace period. It never engraves, but becomes Seasoned after half a year
/// of good adherence. Vacation days do not count towards the gap.
/// </summary>
internal sealed class CapacityTracker(DateOnly masteredOn, int dose) : IRetentionTracker
{
    // Rounded down, so practising whenever the node is due meets the weekly dose.
    private readonly int intervalDays = Math.Max(1, 7 / dose);
    private readonly Dictionary<DateOnly, int> sessionsByWeek = [];
    private readonly HashSet<DateOnly> pausedWeeks = [];
    private int gapDays;
    private DateOnly? lastSession;
    private DateOnly? seasonedOn;
    private DateOnly? lastAdvanced;

    public bool IsFortified => seasonedOn is not null;

    /// <summary>Days since the last session, counting the next day.</summary>
    private int DaysSinceSession => gapDays + 1;

    public bool IsDue(DateOnly day) => DaysSinceSession >= intervalDays;

    public double Severity(DateOnly day) =>
        DaysSinceSession <= RetentionRules.CapacityGraceDays
            ? 0
            : RetentionRules.MissSeverity(DaysSinceSession - RetentionRules.CapacityGraceDays - 1);

    public bool Defends(DayPractice practice) => practice.Entries > 0;

    public void Advance(DateOnly day, DayPractice practice, bool paused)
    {
        lastAdvanced = day;
        var week = WeekStart(day);
        if (paused)
            pausedWeeks.Add(week);

        if (practice.Entries > 0)
        {
            lastSession = day;
            gapDays = 0;
            sessionsByWeek[week] = sessionsByWeek.GetValueOrDefault(week) + 1;
        }
        else if (!paused && (lastSession is not null || day >= masteredOn))
        {
            gapDays++;
        }

        if (seasonedOn is null &&
            day.DayNumber - masteredOn.DayNumber >= RetentionRules.SeasonedDays &&
            Adherence(day) >= RetentionRules.SeasonedAdherence)
        {
            seasonedOn = day;
        }
    }

    public void Miss(DateOnly day)
    {
    }

    public RetentionStatus Status(DateOnly today)
    {
        var maintained = Math.Max(0, today.DayNumber - masteredOn.DayNumber);
        EngraveCriterion[] criteria =
        [
            new(EngraveCriterionKind.DaysMaintained, maintained, RetentionRules.SeasonedDays),
            new(EngraveCriterionKind.Adherence, Adherence(lastAdvanced ?? masteredOn), RetentionRules.SeasonedAdherence),
        ];

        var seasoned = IsFortified;
        var estimate = masteredOn.AddDays(RetentionRules.SeasonedDays);
        return new RetentionStatus(
            RetentionModel.Capacity,
            seasoned,
            IsEngraved: false,
            seasoned ? 1 : criteria.Average(criterion => criterion.Fraction),
            criteria,
            seasoned ? null : estimate > today ? estimate : today);
    }

    /// <summary>Share of complete weeks since mastery that met the dose; weeks with vacation days are left out.</summary>
    private double Adherence(DateOnly day)
    {
        var met = 0;
        var total = 0;
        for (var week = WeekStart(masteredOn).AddDays(7); week < WeekStart(day); week = week.AddDays(7))
        {
            if (pausedWeeks.Contains(week))
                continue;

            total++;
            if (sessionsByWeek.GetValueOrDefault(week) >= dose)
                met++;
        }

        return total == 0 ? 1 : met / (double)total;
    }

    private static DateOnly WeekStart(DateOnly day) => day.AddDays(-(((int)day.DayOfWeek + 6) % 7));
}
