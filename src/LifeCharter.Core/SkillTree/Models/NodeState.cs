namespace LifeCharter.Core.SkillTree.Models;

/// <summary>Derived state of a node. Phase 2 adds Fortified, Engraved and Corrupted.</summary>
public enum NodeState
{
    Locked,
    Available,
    Training,
    Mastered,
}
