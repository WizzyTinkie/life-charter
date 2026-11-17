using LifeCharter.Core.Progression.Models;

namespace LifeCharter.Core.Progression.Business;

/// <summary>XP needed to go from level L to L+1 is <c>100 × L^1.5</c>: 100, 283, 520, 800, …</summary>
public static class LevelCurve
{
    public static int XpToNextLevel(int level) => (int)Math.Round(100 * Math.Pow(level, 1.5));

    public static LevelInfo LevelFor(int totalXp)
    {
        var level = 1;
        var remaining = Math.Max(0, totalXp);
        while (remaining >= XpToNextLevel(level))
        {
            remaining -= XpToNextLevel(level);
            level++;
        }

        return new LevelInfo(level, remaining, XpToNextLevel(level));
    }
}
