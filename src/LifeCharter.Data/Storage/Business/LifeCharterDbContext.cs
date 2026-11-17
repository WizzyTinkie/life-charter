using System.Linq.Expressions;
using LifeCharter.Core.Goals.Models;
using LifeCharter.Core.Journal.Models;
using LifeCharter.Core.Progression.Models;
using LifeCharter.Core.SkillTree.Models;
using LifeCharter.Core.Sync.Business;
using LifeCharter.Core.Sync.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace LifeCharter.Data.Storage.Business;

/// <summary>
/// The working database of one device. On save it stamps device and HLC on every synced row,
/// turns deletes of mutable rows into tombstones and refuses edits of append-only rows.
/// </summary>
public sealed class LifeCharterDbContext(DbContextOptions<LifeCharterDbContext> options, HybridLogicalClock clock)
    : DbContext(options)
{
    public DbSet<SkillNode> Nodes => Set<SkillNode>();
    public DbSet<SkillEdge> Edges => Set<SkillEdge>();
    public DbSet<Goal> Goals => Set<Goal>();
    public DbSet<Quest> Quests => Set<Quest>();
    public DbSet<LogEntry> Entries => Set<LogEntry>();
    public DbSet<Allocation> Allocations => Set<Allocation>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess) =>
        throw new NotSupportedException("Use SaveChangesAsync (see the async rules in CODING_GUIDELINES.md).");

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        StampSyncMetadata();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder) =>
        // Enums as text: readable in the database and stable if enum members are reordered.
        configurationBuilder.Properties<Enum>().HaveConversion<string>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SkillNode>().ToTable("nodes");
        modelBuilder.Entity<SkillEdge>().ToTable("edges");
        modelBuilder.Entity<Goal>().ToTable("goals");
        modelBuilder.Entity<Quest>().ToTable("quests");
        modelBuilder.Entity<LogEntry>().ToTable("log_entries");
        modelBuilder.Entity<Allocation>().ToTable("allocations");

        modelBuilder.Entity<SkillEdge>().HasIndex(edge => edge.FromNodeId);
        modelBuilder.Entity<SkillEdge>().HasIndex(edge => edge.ToNodeId);
        modelBuilder.Entity<Goal>().HasIndex(goal => goal.NodeId);
        modelBuilder.Entity<Quest>().HasIndex(quest => quest.NodeId);
        modelBuilder.Entity<LogEntry>().HasIndex(entry => entry.NodeId);
        modelBuilder.Entity<Allocation>().HasIndex(allocation => allocation.NodeId);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
                property.SetColumnName(SnakeCaseNaming.Convert(property.Name));

            if (typeof(MutableEntity).IsAssignableFrom(entityType.ClrType))
                entityType.SetQueryFilter(NotDeletedFilter(entityType.ClrType));
        }
    }

    private static LambdaExpression NotDeletedFilter(Type entityType)
    {
        var parameter = Expression.Parameter(entityType, "row");
        var deletedAt = Expression.Property(parameter, nameof(MutableEntity.DeletedAt));
        return Expression.Lambda(Expression.Equal(deletedAt, Expression.Constant(null, typeof(string))), parameter);
    }

    private void StampSyncMetadata()
    {
        foreach (var entry in ChangeTracker.Entries<SyncedEntity>().ToList())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    StampInsert(entry);
                    break;
                case EntityState.Modified when entry.Entity is MutableEntity mutable:
                    mutable.UpdatedAt = clock.Now().ToString();
                    mutable.DeviceId = clock.DeviceId;
                    break;
                case EntityState.Deleted when entry.Entity is MutableEntity mutable:
                    var stamp = clock.Now().ToString();
                    entry.State = EntityState.Modified;
                    mutable.DeletedAt = stamp;
                    mutable.UpdatedAt = stamp;
                    mutable.DeviceId = clock.DeviceId;
                    break;
                case EntityState.Modified or EntityState.Deleted:
                    throw new InvalidOperationException(
                        $"{entry.Entity.GetType().Name} is append-only; add a new row instead of changing or deleting one.");
            }
        }
    }

    private void StampInsert(EntityEntry<SyncedEntity> entry)
    {
        var stamp = clock.Now().ToString();
        entry.Entity.DeviceId = clock.DeviceId;
        switch (entry.Entity)
        {
            case MutableEntity mutable:
                mutable.UpdatedAt = stamp;
                break;
            case AppendOnlyEntity appendOnly:
                appendOnly.CreatedAt = stamp;
                break;
        }
    }
}
