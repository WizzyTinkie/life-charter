using System.Text.Json;
using Avalonia.Threading;
using LifeCharter.App.Notifications.Models;
using LifeCharter.Core.Common.Business;
using LifeCharter.Core.Siege.Models;
using LifeCharter.Core.SkillTree.Models;
using LifeCharter.Platform.Notifications.Interfaces;
using LifeCharter.Platform.Notifications.Models;
using LifeCharter.Platform.Storage.Business;
using Microsoft.Extensions.Logging;

namespace LifeCharter.App.Notifications.Business;

/// <summary>
/// The morning threat report and the evening reminder (PLAN.md section 5). Each is shown at most
/// once a day per device; the evening reminder can be snoozed for an hour.
/// </summary>
public sealed class ThreatReporter(ChartSession session, INotifier notifier, Action openDefense, ILogger<ThreatReporter> logger)
{
    public const int MorningHour = 8;
    public const int EveningHour = 20;
    private const string MorningTag = "morning";
    private const string EveningTag = "evening";
    private const string DefendKey = "defend";
    private const string SnoozeKey = "snooze";
    private const int MaxListedNodes = 5;
    private static readonly TimeSpan SnoozeTime = TimeSpan.FromHours(1);

    private NotificationState state = new();
    private DateTimeOffset? snoozedUntil;

    private static string StatePath => Path.Combine(AppPaths.StateDirectory, "notifications.json");

    public async Task StartAsync()
    {
        state = await LoadAsync();
        notifier.ActionInvoked += OnActionInvoked;
        await CheckAsync();
    }

    /// <summary>Shows whatever is due now. Call it periodically.</summary>
    public async Task CheckAsync()
    {
        var siege = session.Progression.Siege;
        if (!siege.IsStarted || siege.Today.Kind != SiegeDayKind.Siege)
            return;

        var now = DateTimeOffset.Now;
        var today = session.Today;
        var nodes = session.Chart.Nodes.ToDictionary(node => node.Id);
        var attacks = siege.Today.Attacks;

        if (now.Hour >= MorningHour && state.LastMorningReport != today && attacks.Count > 0)
        {
            var names = attacks.Select(attack => nodes[attack.NodeId]).Select(node =>
                node.RetentionModel == RetentionModel.Memory ? $"{node.Title} (recall)" : node.Title);
            await ShowAsync(new AppNotification(
                MorningTag,
                attacks.Count == 1 ? "1 skill under attack today" : $"{attacks.Count} skills under attack today",
                List(names) + " — defend them from the widget, the tray or quick entry.",
                [new(NotificationAction.DefaultKey, "Defend"), new(DefendKey, "Defend")]));
            state = state with { LastMorningReport = today };
            await SaveAsync();
        }

        var open = attacks.Where(attack => !attack.Defended && attack.Threat > 0).ToList();
        if (now.Hour >= EveningHour && state.LastEveningReminder != today && open.Count > 0 && (snoozedUntil is null || now >= snoozedUntil))
        {
            await ShowAsync(new AppNotification(
                EveningTag,
                open.Count == 1 ? "1 attack still undefended" : $"{open.Count} attacks still undefended",
                $"−{open.Sum(attack => attack.Threat)} HP at midnight: {List(open.Select(attack => nodes[attack.NodeId].Title))}",
                [new(NotificationAction.DefaultKey, "Defend"), new(DefendKey, "Defend"), new(SnoozeKey, "In 1 hour")]));
            snoozedUntil = null;
            state = state with { LastEveningReminder = today };
            await SaveAsync();
        }
    }

    private async Task ShowAsync(AppNotification notification)
    {
        if (await notifier.ShowAsync(notification))
            logger.LogInformation("Notification {Tag}: {Summary}", notification.Tag, notification.Summary);
    }

    private void OnActionInvoked(object? sender, NotificationActionEventArgs e) =>
        Dispatcher.UIThread.Post(async () =>
        {
            if (e.ActionKey == SnoozeKey)
            {
                snoozedUntil = DateTimeOffset.Now + SnoozeTime;
                state = state with { LastEveningReminder = null };
                await SaveAsync();
            }
            else
            {
                openDefense();
            }
        });

    private static string List(IEnumerable<string> names)
    {
        var all = names.ToList();
        return all.Count <= MaxListedNodes
            ? string.Join(", ", all)
            : string.Join(", ", all.Take(MaxListedNodes)) + $" and {all.Count - MaxListedNodes} more";
    }

    private async Task<NotificationState> LoadAsync()
    {
        try
        {
            if (File.Exists(StatePath))
            {
                await using var stream = File.OpenRead(StatePath);
                return await JsonSerializer.DeserializeAsync<NotificationState>(stream) ?? new NotificationState();
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not read {Path}", StatePath);
        }

        return new NotificationState();
    }

    private async Task SaveAsync()
    {
        try
        {
            Directory.CreateDirectory(AppPaths.StateDirectory);
            var temp = StatePath + ".tmp";
            await File.WriteAllTextAsync(temp, JsonSerializer.Serialize(state));
            File.Move(temp, StatePath, overwrite: true);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not write {Path}", StatePath);
        }
    }
}
