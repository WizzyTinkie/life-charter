namespace LifeCharter.Platform.Notifications.Models;

/// <summary>A button on a notification. The key "default" is a click on the notification itself.</summary>
public sealed record NotificationAction(string Key, string Label)
{
    public const string DefaultKey = "default";
}
