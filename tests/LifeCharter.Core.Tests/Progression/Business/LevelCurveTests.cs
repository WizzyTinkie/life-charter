using LifeCharter.Core.Progression.Business;

namespace LifeCharter.Core.Tests.Progression.Business;

public sealed class LevelCurveTests
{
    [Theory]
    [InlineData(0, 1, 0, 100)]
    [InlineData(99, 1, 99, 100)]
    [InlineData(100, 2, 0, 283)]
    [InlineData(383, 3, 0, 520)]
    [InlineData(-5, 1, 0, 100)]
    public void Level_follows_the_curve(int totalXp, int level, int xpIntoLevel, int xpForNext)
    {
        var info = LevelCurve.LevelFor(totalXp);

        Assert.Equal(level, info.Level);
        Assert.Equal(xpIntoLevel, info.XpIntoLevel);
        Assert.Equal(xpForNext, info.XpForNextLevel);
    }
}
