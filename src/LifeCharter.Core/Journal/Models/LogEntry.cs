using LifeCharter.Core.Retention.Models;
using LifeCharter.Core.Sync.Models;

namespace LifeCharter.Core.Journal.Models;

/// <summary>
/// Something you did. Append-only: an undo is a new entry whose <see cref="RetractsEntryId"/>
/// points at the entry it cancels, never an edit or delete.
/// </summary>
public sealed class LogEntry : AppendOnlyEntity
{
    public Guid NodeId { get; set; }

    /// <summary>Goal this entry counts towards; when null it counts for the node's goals with a matching unit.</summary>
    public Guid? GoalId { get; set; }

    public Guid? QuestId { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public double Amount { get; set; } = 1;
    public string Unit { get; set; } = "";
    public string Note { get; set; } = "";
    public EntrySource Source { get; set; } = EntrySource.Manual;

    /// <summary>
    /// Set on a recall session (a review of a Memory or Motor skill node). Rated entries defend
    /// and earn XP but do not count towards goals.
    /// </summary>
    public ReviewRating? Rating { get; set; }

    /// <summary>When set, this entry only cancels the entry with that id.</summary>
    public Guid? RetractsEntryId { get; set; }
}
