using LifeCharter.Core.Retention.Models;

namespace LifeCharter.Core.Retention.Interfaces;

/// <summary>
/// Follows one node's retention model day by day. For every day, in order, the siege engine
/// first asks whether the node needs defending (judged from earlier days only), then feeds it
/// that day's practice and, when an attack stayed undefended, the miss.
/// </summary>
public interface IRetentionTracker
{
    /// <summary>Fortified (Seasoned for Capacity) nodes take half damage.</summary>
    bool IsFortified { get; }

    /// <summary>Whether the node needs practice on <paramref name="day"/>.</summary>
    bool IsDue(DateOnly day);

    /// <summary>Damage multiplier if an attack on <paramref name="day"/> stays undefended; 0 is harmless.</summary>
    double Severity(DateOnly day);

    /// <summary>Whether this practice counts as a defense of the node.</summary>
    bool Defends(DayPractice practice);

    /// <summary>Feeds one day. Called for every day in order, starting before mastery.</summary>
    void Advance(DateOnly day, DayPractice practice, bool paused);

    /// <summary>Records an undefended attack on the day just advanced.</summary>
    void Miss(DateOnly day);

    RetentionStatus Status(DateOnly today);
}
