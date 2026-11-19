namespace LifeCharter.Core.Retention.Models;

public enum EngraveCriterionKind
{
    /// <summary>FSRS memory stability in days.</summary>
    Stability,

    /// <summary>Successful recall sessions.</summary>
    Reviews,

    /// <summary>Days between the first and the last counted practice.</summary>
    SpanDays,

    /// <summary>Habit repetitions after misses are subtracted.</summary>
    Repetitions,

    /// <summary>Average answer of the latest automaticity self-check (1–5).</summary>
    SelfCheck,

    /// <summary>Practice sessions spaced at least a few days apart.</summary>
    SpacedSessions,

    /// <summary>Days kept since mastery (Capacity: towards Seasoned).</summary>
    DaysMaintained,

    /// <summary>Share of weeks that met the maintenance dose.</summary>
    Adherence,
}
