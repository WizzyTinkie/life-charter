using Avalonia.Controls;
using LifeCharter.App.Logging.Business;
using LifeCharter.Platform.DesktopWidget.Business;
using LifeCharter.Platform.Shortcuts.Business;
using LifeCharter.Platform.Shortcuts.Interfaces;
using LifeCharter.Platform.Shortcuts.Models;

namespace LifeCharter.App.Shell.Business;

/// <summary>Which global shortcuts LifeCharter registers, and with what, per platform.</summary>
public static class GlobalShortcutSetup
{
    public const string QuickEntry = "quick-entry";
    public const string Peek = "peek";

    // Win+Shift+Space switches the keyboard layout on Windows, so Windows uses Win+Alt.
    private static readonly ShortcutModifiers Modifiers = OperatingSystem.IsWindows()
        ? ShortcutModifiers.Meta | ShortcutModifiers.Alt
        : ShortcutModifiers.Meta | ShortcutModifiers.Shift;

    public static IReadOnlyList<GlobalShortcut> Defaults { get; } =
    [
        new(QuickEntry, "Quick entry", Modifiers, ' '),
        new(Peek, "Peek widget", Modifiers, 'W'),
    ];

    /// <summary>The platform's shortcut service, or null where none is available (other Linux desktops: bind <c>lifecharter quick</c> yourself).</summary>
    public static IGlobalShortcuts? Create(Window host)
    {
        if (OperatingSystem.IsWindows())
            return new WindowsShortcuts(host);

        return KWinRuleInstaller.IsKdePlasma ? new KGlobalAccelShortcuts(AppLogging.CreateLogger<KGlobalAccelShortcuts>()) : null;
    }
}
