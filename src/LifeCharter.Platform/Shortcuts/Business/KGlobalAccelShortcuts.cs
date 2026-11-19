using LifeCharter.Platform.Common.Business;
using LifeCharter.Platform.Shortcuts.Interfaces;
using LifeCharter.Platform.Shortcuts.Models;
using Microsoft.Extensions.Logging;
using Tmds.DBus.Protocol;

namespace LifeCharter.Platform.Shortcuts.Business;

/// <summary>
/// Global shortcuts on KDE Plasma through kglobalaccel over D-Bus, the way KDE apps register
/// theirs. They appear under "LifeCharter" in System Settings → Shortcuts, where the user can
/// change them; a changed shortcut is kept. They work while LifeCharter runs.
/// </summary>
public sealed class KGlobalAccelShortcuts(ILogger<KGlobalAccelShortcuts> logger) : IGlobalShortcuts
{
    private const string Service = "org.kde.kglobalaccel";
    private const string Interface = "org.kde.KGlobalAccel";
    private const string ComponentId = "lifecharter";
    private const string ComponentName = "LifeCharter";

    // kglobalaccel setShortcut flags.
    private const uint IsDefault = 1;
    private const uint SetPresent = 2;

    // Qt key codes and modifiers, the format kglobalaccel stores.
    private const int QtShift = 0x02000000;
    private const int QtControl = 0x04000000;
    private const int QtAlt = 0x08000000;
    private const int QtMeta = 0x10000000;

    private readonly List<GlobalShortcut> registered = [];
    private DBusConnection? connection;
    private IDisposable? subscription;

    public event EventHandler<string>? Pressed;

    public async Task<IReadOnlyList<GlobalShortcut>> RegisterAsync(IReadOnlyList<GlobalShortcut> shortcuts, CancellationToken cancellationToken = default)
    {
        try
        {
            connection ??= await SessionBus.ConnectAsync().ConfigureAwait(false);
            foreach (var shortcut in shortcuts)
            {
                var actionId = ActionId(shortcut);
                int[] keys = [QtKey(shortcut)];
                await SessionBus.CallAsync(connection, Service, "/kglobalaccel", Interface, "doRegister", "as",
                    (ref MessageWriter writer) => writer.WriteArray(actionId)).ConfigureAwait(false);

                // Default first, then the active shortcut: kglobalaccel keeps one the user changed.
                await SetShortcutAsync(actionId, keys, SetPresent | IsDefault).ConfigureAwait(false);
                await SetShortcutAsync(actionId, keys, SetPresent).ConfigureAwait(false);
                registered.Add(shortcut);
            }

            subscription ??= await connection.AddMatchAsync(
                new MatchRule
                {
                    Type = MessageType.Signal,
                    Path = "/component/" + ComponentId,
                    Interface = "org.kde.kglobalaccel.Component",
                    Member = "globalShortcutPressed",
                },
                (message, _) =>
                {
                    var reader = message.GetBodyReader();
                    reader.ReadString();
                    return reader.ReadString();
                },
                notification =>
                {
                    if (notification.HasValue)
                        Pressed?.Invoke(this, notification.Value);
                },
                emitOnCapturedContext: false).ConfigureAwait(false);

            logger.LogInformation("Global shortcuts registered with kglobalaccel: {Shortcuts}", string.Join(", ", registered.Select(s => $"{s.Id} ({s})")));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not register global shortcuts with kglobalaccel");
        }

        return registered;
    }

    public async ValueTask DisposeAsync()
    {
        if (connection is not null)
        {
            foreach (var shortcut in registered)
            {
                try
                {
                    var actionId = ActionId(shortcut);
                    await SessionBus.CallAsync(connection, Service, "/kglobalaccel", Interface, "setInactive", "as",
                        (ref MessageWriter writer) => writer.WriteArray(actionId)).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    logger.LogDebug(ex, "Could not deactivate shortcut {Id}", shortcut.Id);
                }
            }
        }

        subscription?.Dispose();
        connection?.Dispose();
    }

    internal static int QtKey(GlobalShortcut shortcut) =>
        char.ToUpperInvariant(shortcut.Key) |
        (shortcut.Modifiers.HasFlag(ShortcutModifiers.Shift) ? QtShift : 0) |
        (shortcut.Modifiers.HasFlag(ShortcutModifiers.Control) ? QtControl : 0) |
        (shortcut.Modifiers.HasFlag(ShortcutModifiers.Alt) ? QtAlt : 0) |
        (shortcut.Modifiers.HasFlag(ShortcutModifiers.Meta) ? QtMeta : 0);

    private Task SetShortcutAsync(string[] actionId, int[] keys, uint flags) =>
        SessionBus.CallAsync(connection!, Service, "/kglobalaccel", Interface, "setShortcut", "asaiu",
            (ref MessageWriter writer) =>
            {
                writer.WriteArray(actionId);
                writer.WriteArray(keys);
                writer.WriteUInt32(flags);
            },
            (message, _) => message.GetBodyReader().ReadArrayOfInt32());

    private static string[] ActionId(GlobalShortcut shortcut) => [ComponentId, shortcut.Id, ComponentName, shortcut.Label];
}
