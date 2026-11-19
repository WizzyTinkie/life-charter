using LifeCharter.Core.Sync.Models;

namespace LifeCharter.Core.Siege.Models;

/// <summary>Days without attacks (both dates included). Ending a vacation early moves <see cref="EndDate"/>.</summary>
public sealed class Vacation : MutableEntity
{
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
}
