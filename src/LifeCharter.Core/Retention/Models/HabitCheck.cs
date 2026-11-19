using LifeCharter.Core.Sync.Models;

namespace LifeCharter.Core.Retention.Models;

/// <summary>
/// Self-Report Behavioural Automaticity Index (Gardner 2012): four statements about a habit,
/// each answered from 1 (strongly disagree) to 5 (strongly agree). A Habit node engraves only
/// after a check averaging 4 or more.
/// </summary>
public sealed class HabitCheck : AppendOnlyEntity
{
    public Guid NodeId { get; set; }
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>"… is something I do automatically."</summary>
    public int Automatically { get; set; }

    /// <summary>"… is something I do without having to consciously remember."</summary>
    public int WithoutRemembering { get; set; }

    /// <summary>"… is something I do without thinking."</summary>
    public int WithoutThinking { get; set; }

    /// <summary>"… is something I start doing before I realize I'm doing it."</summary>
    public int BeforeRealizing { get; set; }
}
