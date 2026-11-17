using LifeCharter.Core.SkillTree.Models;

namespace LifeCharter.Core.Progression.Business;

/// <summary>Tunable numbers of the progression loop (PLAN.md section 3).</summary>
public static class ProgressionRules
{
    public const int StartingSkillPoints = 3;
    public const int EntryXp = 10;

    /// <summary>Entries per goal and day that earn XP; more still count for the goal, not for XP.</summary>
    public const int DailyEntryCapPerGoal = 3;

    /// <summary>Nodes in training per attribute.</summary>
    public const int WorkInProgressLimit = 3;

    public static int DefaultXpReward(NodeKind kind) => kind switch
    {
        NodeKind.Small => 50,
        NodeKind.Notable => 150,
        NodeKind.Keystone => 500,
        _ => 0,
    };
}
