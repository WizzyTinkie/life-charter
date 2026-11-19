using System.Runtime.Versioning;
using Avalonia.Controls;
using LifeCharter.Platform.Shortcuts.Business;
using LifeCharter.Platform.Shortcuts.Interfaces;
using LifeCharter.Platform.Shortcuts.Models;

namespace LifeCharter.App.Shell.Business;

/// <summary>Global hot keys on Windows, delivered to the desktop widget's window procedure.</summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsShortcuts(Window host) : IGlobalShortcuts
{
    private readonly Dictionary<int, GlobalShortcut> byId = [];
    private nint handle;

    public event EventHandler<string>? Pressed;

    public Task<IReadOnlyList<GlobalShortcut>> RegisterAsync(IReadOnlyList<GlobalShortcut> shortcuts, CancellationToken cancellationToken = default)
    {
        if (host.TryGetPlatformHandle()?.Handle is not { } window)
            return Task.FromResult<IReadOnlyList<GlobalShortcut>>([]);

        handle = window;
        Win32Properties.AddWndProcHookCallback(host, OnMessage);
        for (var i = 0; i < shortcuts.Count; i++)
        {
            if (WindowsHotKeys.Register(window, i + 1, shortcuts[i]))
                byId[i + 1] = shortcuts[i];
        }

        return Task.FromResult<IReadOnlyList<GlobalShortcut>>(byId.Values.ToList());
    }

    public ValueTask DisposeAsync()
    {
        foreach (var id in byId.Keys)
            WindowsHotKeys.Unregister(handle, id);

        return ValueTask.CompletedTask;
    }

    private nint OnMessage(nint window, uint message, nint wParam, nint lParam, ref bool handled)
    {
        if (message == WindowsHotKeys.HotKeyMessage && byId.TryGetValue((int)wParam, out var shortcut))
        {
            Pressed?.Invoke(this, shortcut.Id);
            handled = true;
        }

        return 0;
    }
}
