namespace LifeCharter.Core.Goals.Models;

public enum GoalMetric
{
    /// <summary>Each log entry counts as 1.</summary>
    Count,

    /// <summary>Log entry amounts are added up (km, minutes, pages, litres…).</summary>
    Sum,

    /// <summary>Done once; a single entry completes it.</summary>
    Milestone,
}
