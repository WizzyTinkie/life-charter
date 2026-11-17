using LifeCharter.Core.Sync.Models;

namespace LifeCharter.Core.Goals.Models;

/// <summary>A measurable target attached to a node. A node is mastered when all its goals are complete.</summary>
public sealed class Goal : MutableEntity
{
    public Guid NodeId { get; set; }
    public string Title { get; set; } = "";
    public GoalMetric Metric { get; set; } = GoalMetric.Sum;
    public double Target { get; set; } = 1;
    public string Unit { get; set; } = "";
    public GoalWindow Window { get; set; } = GoalWindow.Total;

    /// <summary>For daily and weekly goals: how many periods must meet the target.</summary>
    public int Periods { get; set; } = 1;

    /// <summary>For daily and weekly goals: the periods must follow each other without a gap.</summary>
    public bool Consecutive { get; set; }

    public DateOnly StartDate { get; set; }
    public DateOnly? Deadline { get; set; }
    public GoalSource Source { get; set; } = GoalSource.Manual;
}
