using LifeCharter.Core.Sync.Models;

namespace LifeCharter.Core.Siege.Models;

/// <summary>A ward spent to shield one whole day from damage (illness, travel).</summary>
public sealed class WardUse : AppendOnlyEntity
{
    public DateOnly Date { get; set; }
}
