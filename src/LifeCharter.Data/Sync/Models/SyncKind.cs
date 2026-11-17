namespace LifeCharter.Data.Sync.Models;

public enum SyncKind
{
    /// <summary>Rows are never edited after insert; merging is a set union by <c>id</c>.</summary>
    AppendOnly,

    /// <summary>Whole-row last-writer-wins by the HLC text in <c>updated_at</c>.</summary>
    LastWriterWins,
}
