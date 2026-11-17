using LifeCharter.Core.Sync.Models;
using LifeCharter.Data.Sync.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace LifeCharter.Data.Sync.Business;

/// <summary>Derives the snapshot merge schema from the EF model, so new tables and columns sync automatically.</summary>
public static class SyncSchemaBuilder
{
    public static IReadOnlyList<SyncedTable> FromModel(IModel model) =>
        model.GetEntityTypes()
            .Where(entityType => typeof(SyncedEntity).IsAssignableFrom(entityType.ClrType))
            .Select(entityType => new SyncedTable(
                entityType.GetTableName() ?? throw new InvalidOperationException($"{entityType.Name} has no table."),
                typeof(MutableEntity).IsAssignableFrom(entityType.ClrType) ? SyncKind.LastWriterWins : SyncKind.AppendOnly,
                entityType.GetProperties().Select(property => property.GetColumnName()).ToList()))
            .OrderBy(table => table.Name, StringComparer.Ordinal)
            .ToList();
}
