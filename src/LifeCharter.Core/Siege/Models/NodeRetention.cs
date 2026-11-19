using LifeCharter.Core.Retention.Models;

namespace LifeCharter.Core.Siege.Models;

/// <summary>Retention and siege state of a mastered node.</summary>
public sealed record NodeRetention(
    Guid NodeId,
    DateOnly MasteredOn,
    RetentionStatus Status,
    int Integrity,
    bool IsCorrupted,
    bool RewardWithheld,
    int CleanseDone,
    int CleanseRequired,
    int TimesCorrupted);
