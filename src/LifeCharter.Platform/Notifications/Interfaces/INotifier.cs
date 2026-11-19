using LifeCharter.Platform.Notifications.Models;

namespace LifeCharter.Platform.Notifications.Interfaces;

public interface INotifier : IAsyncDisposable
{
    /// <summary>Raised, on a worker thread, when the user clicks the notification or one of its buttons.</summary>
    event EventHandler<NotificationActionEventArgs>? ActionInvoked;

    /// <returns><c>false</c> when the notification could not be shown.</returns>
    Task<bool> ShowAsync(AppNotification notification, CancellationToken cancellationToken = default);
}
