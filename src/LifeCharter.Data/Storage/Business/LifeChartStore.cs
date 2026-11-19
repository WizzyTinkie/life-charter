using LifeCharter.Core.Common.Interfaces;
using LifeCharter.Core.Common.Models;
using LifeCharter.Core.Goals.Models;
using LifeCharter.Core.Journal.Models;
using LifeCharter.Core.Progression.Models;
using LifeCharter.Core.Retention.Models;
using LifeCharter.Core.Siege.Models;
using LifeCharter.Core.SkillTree.Models;
using LifeCharter.Core.Sync.Models;
using Microsoft.EntityFrameworkCore;

namespace LifeCharter.Data.Storage.Business;

/// <summary>
/// <see cref="ILifeChartStore"/> on EF Core. Every call uses its own short-lived context,
/// which suits a desktop app with background sync merging into the same database.
/// </summary>
public sealed class LifeChartStore(IDbContextFactory<LifeCharterDbContext> contextFactory) : ILifeChartStore
{
    public async Task InitializeAsync(LifeChartData template, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await context.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
        await context.Database.ExecuteSqlRawAsync("PRAGMA journal_mode = WAL;", cancellationToken).ConfigureAwait(false);

        if (await context.Nodes.IgnoreQueryFilters().AnyAsync(cancellationToken).ConfigureAwait(false))
            return;

        context.Nodes.AddRange(template.Nodes);
        context.Edges.AddRange(template.Edges);
        context.Goals.AddRange(template.Goals);
        context.Quests.AddRange(template.Quests);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<LifeChartData> LoadAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var nodes = await context.Nodes.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);
        var edges = await context.Edges.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);
        var goals = await context.Goals.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);
        var quests = await context.Quests.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);
        var entries = await context.Entries.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);
        var allocations = await context.Allocations.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);
        var habitChecks = await context.HabitChecks.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);
        var wardUses = await context.WardUses.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);
        var vacations = await context.Vacations.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);
        var siegeSettings = await context.SiegeSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        return new LifeChartData(nodes, edges, goals, quests, entries, allocations)
        {
            HabitChecks = habitChecks,
            WardUses = wardUses,
            Vacations = vacations,
            SiegeSettings = siegeSettings,
        };
    }

    public Task SaveNodeAsync(SkillNode node, CancellationToken cancellationToken = default) =>
        UpsertAsync(node, cancellationToken);

    public async Task DeleteNodeAsync(Guid nodeId, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var node = await context.Nodes.FindAsync([nodeId], cancellationToken).ConfigureAwait(false);
        if (node is null)
            return;

        context.Nodes.Remove(node);
        context.Edges.RemoveRange(await context.Edges
            .Where(edge => edge.FromNodeId == nodeId || edge.ToNodeId == nodeId)
            .ToListAsync(cancellationToken).ConfigureAwait(false));
        context.Goals.RemoveRange(await context.Goals.Where(goal => goal.NodeId == nodeId).ToListAsync(cancellationToken).ConfigureAwait(false));
        context.Quests.RemoveRange(await context.Quests.Where(quest => quest.NodeId == nodeId).ToListAsync(cancellationToken).ConfigureAwait(false));
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task SaveEdgeAsync(SkillEdge edge, CancellationToken cancellationToken = default) =>
        UpsertAsync(edge, cancellationToken);

    public Task DeleteEdgeAsync(Guid edgeId, CancellationToken cancellationToken = default) =>
        DeleteAsync<SkillEdge>(edgeId, cancellationToken);

    public Task SaveGoalAsync(Goal goal, CancellationToken cancellationToken = default) =>
        UpsertAsync(goal, cancellationToken);

    public Task DeleteGoalAsync(Guid goalId, CancellationToken cancellationToken = default) =>
        DeleteAsync<Goal>(goalId, cancellationToken);

    public Task SaveQuestAsync(Quest quest, CancellationToken cancellationToken = default) =>
        UpsertAsync(quest, cancellationToken);

    public Task AddEntryAsync(LogEntry entry, CancellationToken cancellationToken = default) =>
        InsertAsync(entry, cancellationToken);

    public Task AddAllocationAsync(Allocation allocation, CancellationToken cancellationToken = default) =>
        InsertAsync(allocation, cancellationToken);

    public Task AddHabitCheckAsync(HabitCheck check, CancellationToken cancellationToken = default) =>
        InsertAsync(check, cancellationToken);

    public Task AddWardUseAsync(WardUse wardUse, CancellationToken cancellationToken = default) =>
        InsertAsync(wardUse, cancellationToken);

    public Task SaveVacationAsync(Vacation vacation, CancellationToken cancellationToken = default) =>
        UpsertAsync(vacation, cancellationToken);

    public Task DeleteVacationAsync(Guid vacationId, CancellationToken cancellationToken = default) =>
        DeleteAsync<Vacation>(vacationId, cancellationToken);

    public Task SaveSiegeSettingsAsync(SiegeSettings settings, CancellationToken cancellationToken = default) =>
        UpsertAsync(settings, cancellationToken);

    private async Task UpsertAsync<T>(T entity, CancellationToken cancellationToken) where T : MutableEntity
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var exists = await context.Set<T>().IgnoreQueryFilters()
            .AnyAsync(row => row.Id == entity.Id, cancellationToken).ConfigureAwait(false);

        if (exists)
            context.Set<T>().Update(entity);
        else
            context.Set<T>().Add(entity);

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task InsertAsync<T>(T entity, CancellationToken cancellationToken) where T : AppendOnlyEntity
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        context.Set<T>().Add(entity);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task DeleteAsync<T>(Guid id, CancellationToken cancellationToken) where T : MutableEntity
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var entity = await context.Set<T>().FindAsync([id], cancellationToken).ConfigureAwait(false);
        if (entity is null)
            return;

        context.Set<T>().Remove(entity);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
