namespace LifeCharter.Core.Goals.Models;

public enum GoalWindow
{
    /// <summary>Everything since the goal started counts towards one target.</summary>
    Total,

    /// <summary>The target must be met on <see cref="Goal.Periods"/> days.</summary>
    Daily,

    /// <summary>The target must be met in <see cref="Goal.Periods"/> weeks (Monday to Sunday).</summary>
    Weekly,
}
