using LifeCharter.Core.Sync.Models;

namespace LifeCharter.Core.Progression.Models;

/// <summary>Spending (or refunding) a skill point on a node. Replayed in order to get the allocated set.</summary>
public sealed class Allocation : AppendOnlyEntity
{
    public Guid NodeId { get; set; }
    public AllocationAction Action { get; set; }
    public DateTimeOffset Timestamp { get; set; }
}
