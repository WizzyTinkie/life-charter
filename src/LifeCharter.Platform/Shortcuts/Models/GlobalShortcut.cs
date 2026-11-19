namespace LifeCharter.Platform.Shortcuts.Models;

/// <summary>
/// A system-wide shortcut. <see cref="Key"/> is a letter A–Z, a digit or a space; the user can
/// rebind it in the desktop's shortcut settings.
/// </summary>
public sealed record GlobalShortcut(string Id, string Label, ShortcutModifiers Modifiers, char Key)
{
    public override string ToString()
    {
        var parts = new List<string>();
        if (Modifiers.HasFlag(ShortcutModifiers.Meta)) parts.Add("Meta");
        if (Modifiers.HasFlag(ShortcutModifiers.Control)) parts.Add("Ctrl");
        if (Modifiers.HasFlag(ShortcutModifiers.Alt)) parts.Add("Alt");
        if (Modifiers.HasFlag(ShortcutModifiers.Shift)) parts.Add("Shift");
        parts.Add(Key == ' ' ? "Space" : char.ToUpperInvariant(Key).ToString());
        return string.Join('+', parts);
    }
}
