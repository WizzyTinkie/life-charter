namespace LifeCharter.App.Help.Models;

/// <summary>One row of the level table: the step to <see cref="Level"/> and what it brings.</summary>
public sealed record LevelRow(int Level, int StepXp, int TotalXp, int SkillPointsEarned);
