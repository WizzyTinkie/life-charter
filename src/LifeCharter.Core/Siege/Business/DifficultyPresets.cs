using LifeCharter.Core.Siege.Models;

namespace LifeCharter.Core.Siege.Business;

/// <summary>
/// The presets of PLAN.md section 5. Hardcore corrupts like Standard but needs a longer cleanse,
/// instead of un-allocating the node.
/// </summary>
public static class DifficultyPresets
{
    public static IReadOnlyList<DifficultyPreset> All { get; } =
    [
        new(Difficulty.Peaceful, 0, 6, Weekdays.Sunday, 0, ZeroIntegrityEffect.Nothing, 0),
        new(Difficulty.Casual, 0.5, 3, Weekdays.Saturday | Weekdays.Sunday, 5, ZeroIntegrityEffect.Dim, 2),
        new(Difficulty.Standard, 1, 6, Weekdays.Sunday, 3, ZeroIntegrityEffect.Corrupt, 3),
        new(Difficulty.Hardcore, 1.5, 10, Weekdays.None, 1, ZeroIntegrityEffect.Corrupt, 6),
    ];

    public static DifficultyPreset For(Difficulty difficulty) =>
        All.FirstOrDefault(preset => preset.Difficulty == difficulty) ?? For(Difficulty.Standard);

    public static void Apply(SiegeSettings settings, Difficulty difficulty)
    {
        var preset = For(difficulty);
        settings.Difficulty = preset.Difficulty;
        settings.DamageMultiplier = preset.DamageMultiplier;
        settings.AttacksPerDay = preset.AttacksPerDay;
        settings.RestDays = preset.RestDays;
        settings.WardCap = preset.WardCap;
        settings.ZeroIntegrity = preset.ZeroIntegrity;
        settings.CleanseSessions = preset.CleanseSessions;
    }

    /// <summary>The preset whose knobs all match, or Custom.</summary>
    public static Difficulty Detect(SiegeSettings settings) =>
        All.FirstOrDefault(preset =>
            preset.DamageMultiplier.Equals(settings.DamageMultiplier) &&
            preset.AttacksPerDay == settings.AttacksPerDay &&
            preset.RestDays == settings.RestDays &&
            preset.WardCap == settings.WardCap &&
            preset.ZeroIntegrity == settings.ZeroIntegrity &&
            preset.CleanseSessions == settings.CleanseSessions)?.Difficulty ?? Difficulty.Custom;
}
