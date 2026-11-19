namespace LifeCharter.Platform.Notifications.Models;

public sealed class NotificationActionEventArgs(string tag, string actionKey) : EventArgs
{
    public string Tag { get; } = tag;
    public string ActionKey { get; } = actionKey;
}
