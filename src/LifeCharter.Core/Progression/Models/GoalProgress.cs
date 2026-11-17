namespace LifeCharter.Core.Progression.Models;

/// <summary>
/// Progress of one goal. <see cref="CurrentValue"/> is the total (Total window) or the value in
/// the current day/week; <see cref="CompletedPeriods"/> counts periods that met the target.
/// </summary>
public sealed record GoalProgress(
    Guid GoalId,
    double CurrentValue,
    double Target,
    int CompletedPeriods,
    int RequiredPeriods,
    double Fraction,
    bool IsComplete);
