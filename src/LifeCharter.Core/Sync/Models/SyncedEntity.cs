namespace LifeCharter.Core.Sync.Models;

/// <summary>Base for every row that travels in device snapshots.</summary>
public abstract class SyncedEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>Device that wrote the row last.</summary>
    public string DeviceId { get; set; } = "";
}
