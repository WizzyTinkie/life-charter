namespace LifeCharter.Data.Sync.Models;

/// <summary>
/// Describes one table that travels in device snapshots. Every synced table has an
/// <c>id</c> primary key; last-writer-wins tables also have <c>updated_at</c> and use a
/// <c>deleted_at</c> tombstone instead of real deletes.
/// </summary>
public sealed record SyncedTable(string Name, SyncKind Kind, IReadOnlyList<string> Columns)
{
    public const string IdColumn = "id";
    public const string UpdatedAtColumn = "updated_at";

    public void Validate()
    {
        if (!Columns.Contains(IdColumn))
            throw new InvalidOperationException($"Synced table '{Name}' needs an '{IdColumn}' column.");

        if (Kind == SyncKind.LastWriterWins && !Columns.Contains(UpdatedAtColumn))
            throw new InvalidOperationException($"Last-writer-wins table '{Name}' needs an '{UpdatedAtColumn}' column.");
    }
}
