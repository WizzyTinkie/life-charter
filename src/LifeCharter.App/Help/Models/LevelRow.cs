namespace LifeCharter.App.Help.Models;

/// <summary>One row of the level table: the step to <see cref="Level"/> and the training slots it gives.</summary>
public sealed record LevelRow(int Level, int StepXp, int TotalXp, int SkillPointsEarned);
