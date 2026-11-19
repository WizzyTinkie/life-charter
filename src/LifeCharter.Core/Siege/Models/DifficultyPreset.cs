namespace LifeCharter.Core.Siege.Models;

/// <summary>The knobs one difficulty preset sets.</summary>
public sealed record DifficultyPreset(
    Difficulty Difficulty,
    double DamageMultiplier,
    int AttacksPerDay,
    Weekdays RestDays,
    int WardCap,
    ZeroIntegrityEffect ZeroIntegrity,
    int CleanseSessions);
