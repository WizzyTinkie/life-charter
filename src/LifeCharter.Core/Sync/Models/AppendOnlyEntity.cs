namespace LifeCharter.Core.Sync.Models;

/// <summary>A row never edited after insert. Devices merge these as a set union by id.</summary>
public abstract class AppendOnlyEntity : SyncedEntity
{
    /// <summary>HLC stamp of the insert.</summary>
    public string CreatedAt { get; set; } = "";
}
