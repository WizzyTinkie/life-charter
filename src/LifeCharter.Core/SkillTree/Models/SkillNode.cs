using LifeCharter.Core.Sync.Models;

namespace LifeCharter.Core.SkillTree.Models;

public sealed class SkillNode : MutableEntity
{
    public LifeAttribute Attribute { get; set; }
    public NodeKind Kind { get; set; } = NodeKind.Small;
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";

    /// <summary>Comma-separated words the quick-entry parser maps to this node ("water, drink").</summary>
    public string Aliases { get; set; } = "";

    /// <summary>World position on the radial tree canvas.</summary>
    public double X { get; set; }

    public double Y { get; set; }

    public RetentionModel RetentionModel { get; set; } = RetentionModel.Habit;
    public Complexity Complexity { get; set; } = Complexity.Standard;

    /// <summary>Bonus XP granted once when the node is mastered.</summary>
    public int XpReward { get; set; }
}
