namespace LifeCharter.Core.Sync.Models;

/// <summary>
/// A row edited in place. Devices merge it last-writer-wins by <see cref="UpdatedAt"/>
/// (HLC text), and deletes are tombstones in <see cref="DeletedAt"/>.
/// </summary>
public abstract class MutableEntity : SyncedEntity
{
    public string UpdatedAt { get; set; } = "";

    public string? DeletedAt { get; set; }
}
