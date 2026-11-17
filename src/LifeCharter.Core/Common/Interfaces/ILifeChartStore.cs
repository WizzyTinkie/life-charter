using LifeCharter.Core.Common.Models;
using LifeCharter.Core.Goals.Models;
using LifeCharter.Core.Journal.Models;
using LifeCharter.Core.Progression.Models;
using LifeCharter.Core.SkillTree.Models;

namespace LifeCharter.Core.Common.Interfaces;

/// <summary>
/// Persistence for the life chart. Implementations stamp sync metadata (device, HLC) and turn
/// deletes into tombstones; callers never set those fields.
/// </summary>
public interface ILifeChartStore
{
    /// <summary>Creates or upgrades the database and seeds <paramref name="template"/> into an empty one.</summary>
    Task InitializeAsync(LifeChartData template, CancellationToken cancellationToken = default);

    /// <summary>Loads everything that is not deleted.</summary>
    Task<LifeChartData> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveNodeAsync(SkillNode node, CancellationToken cancellationToken = default);

    /// <summary>Deletes a node together with its edges, goals and quests.</summary>
    Task DeleteNodeAsync(Guid nodeId, CancellationToken cancellationToken = default);

    Task SaveEdgeAsync(SkillEdge edge, CancellationToken cancellationToken = default);

    Task DeleteEdgeAsync(Guid edgeId, CancellationToken cancellationToken = default);

    Task SaveGoalAsync(Goal goal, CancellationToken cancellationToken = default);

    Task DeleteGoalAsync(Guid goalId, CancellationToken cancellationToken = default);

    Task SaveQuestAsync(Quest quest, CancellationToken cancellationToken = default);

    Task AddEntryAsync(LogEntry entry, CancellationToken cancellationToken = default);

    Task AddAllocationAsync(Allocation allocation, CancellationToken cancellationToken = default);
}
