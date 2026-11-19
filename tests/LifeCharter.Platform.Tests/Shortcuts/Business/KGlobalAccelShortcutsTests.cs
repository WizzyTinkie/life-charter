using LifeCharter.Platform.Shortcuts.Business;
using LifeCharter.Platform.Shortcuts.Models;

namespace LifeCharter.Platform.Tests.Shortcuts.Business;

public sealed class KGlobalAccelShortcutsTests
{
    [Fact]
    public void Shortcuts_become_qt_key_codes()
    {
        Assert.Equal(0x12000020, KGlobalAccelShortcuts.QtKey(new GlobalShortcut("quick", "Quick entry", ShortcutModifiers.Meta | ShortcutModifiers.Shift, ' ')));
        Assert.Equal(0x12000057, KGlobalAccelShortcuts.QtKey(new GlobalShortcut("peek", "Peek", ShortcutModifiers.Meta | ShortcutModifiers.Shift, 'w')));
    }

    [Fact]
    public void Shortcut_text_reads_like_kde_shows_it() =>
        Assert.Equal("Meta+Shift+Space", new GlobalShortcut("quick", "Quick entry", ShortcutModifiers.Meta | ShortcutModifiers.Shift, ' ').ToString());
}
