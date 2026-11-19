namespace LifeCharter.Core.QuickEntry.Models;

/// <summary>What applying a draft would change, found by computing progression with it.</summary>
public sealed record QuickEntryOutcome(int XpGained, bool Defends, bool Masters, bool LevelUp)
{
    public static QuickEntryOutcome Nothing { get; } = new(0, false, false, false);
}
