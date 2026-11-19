using LifeCharter.Core.Goals.Models;
using LifeCharter.Core.Journal.Models;
using LifeCharter.Core.Progression.Models;
using LifeCharter.Core.Retention.Models;
using LifeCharter.Core.Siege.Models;
using LifeCharter.Core.SkillTree.Models;

namespace LifeCharter.Core.Common.Models;

/// <summary>Everything the progression and siege rules read: the tree, its goals, the append-only history and the siege settings.</summary>
public sealed record LifeChartData(
    IReadOnlyList<SkillNode> Nodes,
    IReadOnlyList<SkillEdge> Edges,
    IReadOnlyList<Goal> Goals,
    IReadOnlyList<Quest> Quests,
    IReadOnlyList<LogEntry> Entries,
    IReadOnlyList<Allocation> Allocations)
{
    public static LifeChartData Empty { get; } = new([], [], [], [], [], []);

    public IReadOnlyList<HabitCheck> HabitChecks { get; init; } = [];
    public IReadOnlyList<WardUse> WardUses { get; init; } = [];
    public IReadOnlyList<Vacation> Vacations { get; init; } = [];

    /// <summary>Null until the siege is switched on.</summary>
    public SiegeSettings? SiegeSettings { get; init; }
}
