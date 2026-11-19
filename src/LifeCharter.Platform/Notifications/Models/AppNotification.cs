namespace LifeCharter.Platform.Notifications.Models;

/// <summary>
/// A desktop notification. A newer notification with the same <see cref="Tag"/> replaces the
/// older one instead of piling up.
/// </summary>
public sealed record AppNotification(string Tag, string Summary, string Body, IReadOnlyList<NotificationAction> Actions);
