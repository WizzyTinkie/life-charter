using LifeCharter.Core.SkillTree.Models;

namespace LifeCharter.Core.Progression.Models;

/// <summary>Derived from the journal and allocations; never stored.</summary>
public sealed record CharacterProgress(
    LevelInfo Level,
    int TotalXp,
    int SkillPoints,
    IReadOnlyDictionary<LifeAttribute, int> XpByAttribute);
