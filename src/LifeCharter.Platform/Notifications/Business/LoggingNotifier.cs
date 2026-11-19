using LifeCharter.Platform.Notifications.Interfaces;
using LifeCharter.Platform.Notifications.Models;
using Microsoft.Extensions.Logging;

namespace LifeCharter.Platform.Notifications.Business;

/// <summary>Stand-in where no notification service is wired up yet (Windows toasts need an app identity): logs instead.</summary>
public sealed class LoggingNotifier(ILogger<LoggingNotifier> logger) : INotifier
{
    public event EventHandler<NotificationActionEventArgs>? ActionInvoked
    {
        add { }
        remove { }
    }

    public Task<bool> ShowAsync(AppNotification notification, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Notification {Tag}: {Summary} — {Body}", notification.Tag, notification.Summary, notification.Body);
        return Task.FromResult(false);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
