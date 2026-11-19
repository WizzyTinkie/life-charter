namespace LifeCharter.Core.Siege.Models;

/// <summary>Everything the siege derives from history, settings and date. Never stored.</summary>
public sealed record SiegeResult(
    bool IsStarted,
    SiegeDay Today,
    SiegeDay? Yesterday,
    int Wards,
    int WardCap,
    int DaysToNextWard,
    int Streak,
    int BestStreak,
    IReadOnlyDictionary<Guid, NodeRetention> Nodes)
{
    public static SiegeResult Empty(DateOnly today) =>
        new(false, new SiegeDay(today, SiegeDayKind.NotStarted, [], false), null, 0, 0, 0, 0, 0, new Dictionary<Guid, NodeRetention>());
}
