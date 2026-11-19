using LifeCharter.Core.Goals.Models;

namespace LifeCharter.Core.Goals.Business;

public static class GoalDefaults
{
    /// <summary>A sensible amount when logging towards a goal without a number: the daily target of a daily sum, otherwise 1.</summary>
    public static double Amount(Goal goal) =>
        goal.Metric == GoalMetric.Sum && goal.Window == GoalWindow.Daily ? goal.Target : 1;
}
