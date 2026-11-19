namespace LifeCharter.Platform.Shortcuts.Models;

[Flags]
public enum ShortcutModifiers
{
    None = 0,
    Shift = 1,
    Control = 2,
    Alt = 4,

    /// <summary>The Super / Windows key.</summary>
    Meta = 8,
}
