using LifeCharter.Platform.Shortcuts.Models;

namespace LifeCharter.Platform.Shortcuts.Interfaces;

public interface IGlobalShortcuts : IAsyncDisposable
{
    /// <summary>Raised, possibly on a worker thread, with the <see cref="GlobalShortcut.Id"/> that was pressed.</summary>
    event EventHandler<string>? Pressed;

    /// <returns>The shortcuts that could be registered.</returns>
    Task<IReadOnlyList<GlobalShortcut>> RegisterAsync(IReadOnlyList<GlobalShortcut> shortcuts, CancellationToken cancellationToken = default);
}
