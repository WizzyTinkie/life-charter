using LifeCharter.Core.Retention.Business;
using LifeCharter.Core.Sync.Models;

namespace LifeCharter.Core.Siege.Models;

/// <summary>
/// Siege configuration, synced between devices so both compute the same attacks. There is one
/// row with a fixed id, so edits on either device merge as last-writer-wins.
/// </summary>
public sealed class SiegeSettings : MutableEntity
{
    public static readonly Guid SingletonId = new("0193a1c4-5e1b-7c47-9a5e-2b8f1d7c5e9e");

    public SiegeSettings() => Id = SingletonId;

    public Difficulty Difficulty { get; set; } = Difficulty.Standard;
    public double DamageMultiplier { get; set; } = 1;

    /// <summary>Daily defense budget: the most attacks on one day. Due nodes beyond it wait without harm.</summary>
    public int AttacksPerDay { get; set; } = 6;

    public Weekdays RestDays { get; set; } = Weekdays.Sunday;
    public int WardCap { get; set; } = 3;
    public ZeroIntegrityEffect ZeroIntegrity { get; set; } = ZeroIntegrityEffect.Corrupt;

    /// <summary>Practice sessions that restore a corrupted node (shorter than relearning: savings effect).</summary>
    public int CleanseSessions { get; set; } = 3;

    public double DesiredRetention { get; set; } = RetentionRules.DefaultDesiredRetention;

    /// <summary>First day with attacks; nothing before it is ever attacked. Null until the siege is switched on.</summary>
    public DateOnly? StartDate { get; set; }
}
