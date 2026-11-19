using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using LifeCharter.Platform.Shortcuts.Models;

namespace LifeCharter.Platform.Shortcuts.Business;

/// <summary>Global hot keys on Windows with <c>RegisterHotKey</c>; <see cref="HotKeyMessage"/> arrives in the window's WndProc.</summary>
[SupportedOSPlatform("windows")]
public static partial class WindowsHotKeys
{
    public const uint HotKeyMessage = 0x0312;

    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;
    private const uint ModWin = 0x0008;
    private const uint ModNoRepeat = 0x4000;

    public static bool Register(nint window, int id, GlobalShortcut shortcut) =>
        RegisterHotKey(window, id, Modifiers(shortcut.Modifiers) | ModNoRepeat, char.ToUpperInvariant(shortcut.Key));

    public static void Unregister(nint window, int id) => UnregisterHotKey(window, id);

    private static uint Modifiers(ShortcutModifiers modifiers) =>
        (modifiers.HasFlag(ShortcutModifiers.Alt) ? ModAlt : 0) |
        (modifiers.HasFlag(ShortcutModifiers.Control) ? ModControl : 0) |
        (modifiers.HasFlag(ShortcutModifiers.Shift) ? ModShift : 0) |
        (modifiers.HasFlag(ShortcutModifiers.Meta) ? ModWin : 0);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool RegisterHotKey(nint window, int id, uint modifiers, uint virtualKey);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool UnregisterHotKey(nint window, int id);
}
