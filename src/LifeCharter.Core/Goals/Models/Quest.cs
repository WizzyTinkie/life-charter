using LifeCharter.Core.Sync.Models;

namespace LifeCharter.Core.Goals.Models;

/// <summary>An actionable, scheduled step that feeds a goal ("run 5 km", "15 min German").</summary>
public sealed class Quest : MutableEntity
{
    public Guid NodeId { get; set; }
    public Guid? GoalId { get; set; }
    public string Title { get; set; } = "";
    public QuestSchedule Schedule { get; set; } = QuestSchedule.Daily;
    public int TimesPerWeek { get; set; }
    public double Amount { get; set; } = 1;
    public string Unit { get; set; } = "";

    /// <summary>Implementation intention: "When/where …, I will …".</summary>
    public string Cue { get; set; } = "";

    /// <summary>Comma-separated words the quick-entry parser maps to this quest.</summary>
    public string Aliases { get; set; } = "";
}
