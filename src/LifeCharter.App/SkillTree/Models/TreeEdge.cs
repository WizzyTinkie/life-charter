namespace LifeCharter.App.SkillTree.Models;

/// <summary>Connection between two nodes, by index into <see cref="TreeLayout.Nodes"/>.</summary>
public readonly record struct TreeEdge(int From, int To);
