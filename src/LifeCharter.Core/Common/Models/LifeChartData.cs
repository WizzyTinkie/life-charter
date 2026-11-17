using LifeCharter.Core.Goals.Models;
using LifeCharter.Core.Journal.Models;
using LifeCharter.Core.Progression.Models;
using LifeCharter.Core.SkillTree.Models;

namespace LifeCharter.Core.Common.Models;

/// <summary>Everything the progression rules read: the tree, its goals and the append-only history.</summary>
public sealed record LifeChartData(
    IReadOnlyList<SkillNode> Nodes,
    IReadOnlyList<SkillEdge> Edges,
    IReadOnlyList<Goal> Goals,
    IReadOnlyList<Quest> Quests,
    IReadOnlyList<LogEntry> Entries,
    IReadOnlyList<Allocation> Allocations)
{
    public static LifeChartData Empty { get; } = new([], [], [], [], [], []);
}
