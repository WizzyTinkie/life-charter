using LifeCharter.Core.SkillTree.Models;

namespace LifeCharter.Core.Retention.Business;

/// <summary>Tunable numbers of the retention models (PLAN.md section 4).</summary>
public static class RetentionRules
{
    // Memory: FSRS stability plus a guard, because all-Easy reviews would reach 365 days in 3 reviews.
    public const double EngravedStabilityDays = 365;
    public const double FortifiedStabilityDays = 90;
    public const int EngravedMinimumReviews = 5;
    public const int EngravedMinimumSpanDays = 182;
    public const double DefaultDesiredRetention = 0.9;

    // Habit: automaticity A(n) = 1 − e^(−k·n) reaches 95 % after the plateau repetitions (Lally 2010).
    public const int HabitPlateauRepetitions = 66;
    public const double HabitEngravedAutomaticity = 0.95;
    public const double HabitFortifiedAutomaticity = 0.70;
    public const double SelfCheckPass = 4;

    // Capacity: detraining starts after about two weeks without the maintenance dose (Spiering 2021).
    public const int CapacityGraceDays = 14;
    public const int SeasonedDays = 182;
    public const double SeasonedAdherence = 0.8;

    // Motor skill: decays slowly (Arthur 1998); engraves after spaced practice following the proficiency test.
    public const double MotorInitialStabilityDays = 7;
    public const int MotorEngravedSessions = 4;
    public const int MotorSessionSpacingDays = 5;
    public const int MotorEngravedSpanDays = 21;

    // Severity of an undefended attack: 1 for a fresh miss, growing to 2.
    public const double MaxSeverity = 2;
    public const double SeverityPerMiss = 0.1;
    public const double SeverityPerRecallPoint = 10;

    public static int HabitRequiredRepetitions(Complexity complexity) => complexity switch
    {
        Complexity.Simple => HabitPlateauRepetitions / 2,
        Complexity.Complex => HabitPlateauRepetitions * 2,
        _ => HabitPlateauRepetitions,
    };

    /// <summary>Modelled automaticity after <paramref name="repetitions"/>; exactly 95 % at <paramref name="required"/>.</summary>
    public static double Automaticity(int repetitions, int required) =>
        1 - Math.Exp(Math.Log(1 - HabitEngravedAutomaticity) / required * repetitions);

    /// <summary>Memory severity: how far predicted recall has fallen below the desired retention.</summary>
    public static double RecallSeverity(double retrievability, double desiredRetention) =>
        Math.Clamp(1 + (desiredRetention - retrievability) * SeverityPerRecallPoint, 1, MaxSeverity);

    /// <summary>Severity that grows with each consecutive miss.</summary>
    public static double MissSeverity(int consecutiveMisses) =>
        Math.Min(MaxSeverity, 1 + SeverityPerMiss * consecutiveMisses);
}
