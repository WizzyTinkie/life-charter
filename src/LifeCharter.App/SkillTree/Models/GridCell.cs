namespace LifeCharter.App.SkillTree.Models;

/// <summary>Cell of the spatial grid used for viewport culling and hit testing.</summary>
public readonly record struct GridCell(int X, int Y);
