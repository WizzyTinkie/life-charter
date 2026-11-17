namespace LifeCharter.App.SkillTree.Models;

/// <summary>A short ring animation on a node, started at <paramref name="StartedAt"/> (stopwatch time).</summary>
public readonly record struct NodePulse(int NodeIndex, TimeSpan StartedAt);
