using LifeCharter.Platform.Common.Business;
using LifeCharter.Platform.Notifications.Interfaces;
using LifeCharter.Platform.Notifications.Models;
using Microsoft.Extensions.Logging;
using Tmds.DBus.Protocol;

namespace LifeCharter.Platform.Notifications.Business;

/// <summary>Desktop notifications on Linux through <c>org.freedesktop.Notifications</c>, with action buttons.</summary>
public sealed class FreedesktopNotifier(ILogger<FreedesktopNotifier> logger) : INotifier
{
    private const string Service = "org.freedesktop.Notifications";
    private const string ObjectPath = "/org/freedesktop/Notifications";
    private const string AppName = "LifeCharter";

    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly Dictionary<uint, string> tagsById = [];
    private readonly Dictionary<string, uint> idsByTag = [];
    private DBusConnection? connection;
    private IDisposable? subscription;

    public event EventHandler<NotificationActionEventArgs>? ActionInvoked;

    public async Task<bool> ShowAsync(AppNotification notification, CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var bus = await EnsureConnectedAsync().ConfigureAwait(false);
            var replaces = idsByTag.GetValueOrDefault(notification.Tag);
            var actions = notification.Actions.SelectMany(action => new[] { action.Key, action.Label }).ToArray();

            var id = await SessionBus.CallAsync(bus, Service, ObjectPath, Service, "Notify", "susssasa{sv}i",
                (ref MessageWriter writer) =>
                {
                    writer.WriteString(AppName);
                    writer.WriteUInt32(replaces);
                    writer.WriteString("");
                    writer.WriteString(notification.Summary);
                    writer.WriteString(notification.Body);
                    writer.WriteArray(actions);
                    var hints = writer.WriteDictionaryStart();
                    writer.WriteDictionaryEntryStart();
                    writer.WriteString("desktop-entry");
                    writer.WriteVariantString("lifecharter");
                    writer.WriteDictionaryEnd(hints);
                    writer.WriteInt32(-1);
                },
                (message, _) => message.GetBodyReader().ReadUInt32()).ConfigureAwait(false);

            tagsById[id] = notification.Tag;
            idsByTag[notification.Tag] = id;
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not show notification {Tag}", notification.Tag);
            return false;
        }
        finally
        {
            gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        subscription?.Dispose();
        connection?.Dispose();
        gate.Dispose();
        await Task.CompletedTask.ConfigureAwait(false);
    }

    private async Task<DBusConnection> EnsureConnectedAsync()
    {
        if (connection is not null)
            return connection;

        var bus = await SessionBus.ConnectAsync().ConfigureAwait(false);
        subscription = await bus.AddMatchAsync(
            new MatchRule { Type = MessageType.Signal, Interface = Service, Path = ObjectPath, Member = "ActionInvoked" },
            (message, _) =>
            {
                var reader = message.GetBodyReader();
                return new InvokedAction(reader.ReadUInt32(), reader.ReadString());
            },
            notification =>
            {
                if (notification.HasValue && tagsById.TryGetValue(notification.Value.Id, out var tag))
                    ActionInvoked?.Invoke(this, new NotificationActionEventArgs(tag, notification.Value.Key));
            },
            emitOnCapturedContext: false).ConfigureAwait(false);

        connection = bus;
        return bus;
    }

    private readonly record struct InvokedAction(uint Id, string Key);
}
