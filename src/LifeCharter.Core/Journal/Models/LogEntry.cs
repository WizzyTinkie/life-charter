using LifeCharter.Core.Sync.Models;

namespace LifeCharter.Core.Journal.Models;

/// <summary>Something you did. Append-only: corrections are new entries, never edits.</summary>
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
}
