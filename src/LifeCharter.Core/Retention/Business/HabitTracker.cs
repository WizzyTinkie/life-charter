using LifeCharter.Core.Retention.Interfaces;
using LifeCharter.Core.Retention.Models;
using LifeCharter.Core.SkillTree.Models;

namespace LifeCharter.Core.Retention.Business;

/// <summary>
/// Automaticity grows with repetitions in a stable context and a single miss does not derail it
/// (Lally 2010), so each undefended attack only costs one repetition. The node engraves at 95 %
/// modelled automaticity plus a passed self-check taken once it is that far.
/// </summary>
internal sealed class HabitTracker(int intervalDays, Complexity complexity) : IRetentionTracker
{
    private readonly int required = RetentionRules.HabitRequiredRepetitions(complexity);
    private int repetitions;
    private int misses;
    private int consecutiveMisses;
    private double? selfCheck;
    private DateOnly? lastPerformed;
    private DateOnly? readyOn;
    private DateOnly? engravedOn;

    private int EffectiveRepetitions => Math.Max(0, repetitions - misses);

    private double Automaticity => RetentionRules.Automaticity(EffectiveRepetitions, required);

    public bool IsFortified => engravedOn is not null || Automaticity >= RetentionRules.HabitFortifiedAutomaticity;

    /// <summary>Due once the usual interval has passed; a fortified habit gets one extra day of slack.</summary>
    public bool IsDue(DateOnly day) =>
        engravedOn is null &&
        (lastPerformed is not { } last || day.DayNumber - last.DayNumber >= intervalDays + (IsFortified ? 1 : 0));

    public double Severity(DateOnly day) => RetentionRules.MissSeverity(consecutiveMisses);

    public bool Defends(DayPractice practice) => practice.Entries > 0;

    public void Advance(DateOnly day, DayPractice practice, bool paused)
    {
        if (practice.Entries > 0)
        {
            repetitions++;
            consecutiveMisses = 0;
            lastPerformed = day;
        }

        if (EffectiveRepetitions >= required)
            readyOn ??= day;

        // A self-check only counts once the repetitions are there; an early one says little.
        if (readyOn is not null)
        {
            foreach (var check in practice.Checks)
                selfCheck = Average(check);
        }

        if (engravedOn is null && readyOn is not null && EffectiveRepetitions >= required && selfCheck >= RetentionRules.SelfCheckPass)
            engravedOn = day;
    }

    public void Miss(DateOnly day)
    {
        misses++;
        consecutiveMisses++;
    }

    public RetentionStatus Status(DateOnly today)
    {
        EngraveCriterion[] criteria =
        [
            new(EngraveCriterionKind.Repetitions, EffectiveRepetitions, required),
            new(EngraveCriterionKind.SelfCheck, selfCheck ?? 0, RetentionRules.SelfCheckPass),
        ];

        var engraved = engravedOn is not null;
        var ready = EffectiveRepetitions >= required;
        return new RetentionStatus(
            RetentionModel.Habit,
            IsFortified,
            engraved,
            engraved ? 1 : RetentionRules.HabitEngravedAutomaticity * Math.Min(1, Automaticity / RetentionRules.HabitEngravedAutomaticity),
            criteria,
            engraved ? null : today.AddDays(Math.Max(0, required - EffectiveRepetitions) * intervalDays),
            NeedsSelfCheck: !engraved && ready);
    }

    public static double Average(HabitCheck check) =>
        (check.Automatically + check.WithoutRemembering + check.WithoutThinking + check.BeforeRealizing) / 4.0;
}
