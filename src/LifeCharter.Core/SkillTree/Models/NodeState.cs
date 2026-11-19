namespace LifeCharter.Core.SkillTree.Models;

/// <summary>Derived state of a node (PLAN.md section 3, node lifecycle).</summary>
public enum NodeState
{
    Locked,
    Available,
    Training,

    /// <summary>Goal reached; the retention phase has begun.</summary>
    Mastered,

    /// <summary>About halfway to permanent; takes half damage. Shown as Seasoned on Capacity nodes.</summary>
    Fortified,

    /// <summary>Learned for good; never attacked again.</summary>
    Engraved,

    /// <summary>Integrity reached 0; restored by a short cleanse.</summary>
    Corrupted,
}
