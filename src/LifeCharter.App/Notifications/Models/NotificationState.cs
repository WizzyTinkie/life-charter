namespace LifeCharter.App.Notifications.Models;

/// <summary>Which daily notifications this device already showed, so a restart does not repeat them.</summary>
public sealed record NotificationState
{
    public DateOnly? LastMorningReport { get; init; }
    public DateOnly? LastEveningReminder { get; init; }
}
