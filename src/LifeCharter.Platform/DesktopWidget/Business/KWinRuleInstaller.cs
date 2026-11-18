using System.Diagnostics;
using LifeCharter.Platform.Storage.Business;
using Microsoft.Extensions.Logging;
using Tmds.DBus.Protocol;

namespace LifeCharter.Platform.DesktopWidget.Business;

/// <summary>
/// Wayland does not let a client keep its own window below others or skip the taskbar.
/// On KDE Plasma we ask KWin to do it with a window rule: plain config in
/// <c>~/.config/kwinrulesrc</c>, followed by a KWin reconfigure over D-Bus.
/// </summary>
public sealed class KWinRuleInstaller(ILogger<KWinRuleInstaller> logger)
{
    private const string ConfigFile = "kwinrulesrc";
    private const string RuleGroup = "lifecharter-desktop-widget";

    // KWin rule policy 2 = Force. String match: 1 = exact. Placement policy 0 = none.
    private const string Force = "2";
    private const string NoPlacement = "0";
    private const string ExactMatch = "1";

    private static readonly TimeSpan ReloadSettleTime = TimeSpan.FromMilliseconds(500);

    private static readonly string[] ObsoleteKeys =
        ["title", "titlematch", "position", "positionrule", "ignoregeometry", "ignoregeometryrule"];

    public static bool IsKdePlasma =>
        OperatingSystem.IsLinux() &&
        (Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP") ?? "").Contains("KDE", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Installs or refreshes the rule for windows whose Wayland app id (window class) is
    /// <paramref name="appId"/>. Writes and reloads KWin only when the rule changed: a reload
    /// racing with the widget's first appearance makes KWin apply the rules late, which leaves
    /// a stale title-bar margin on the fixed-size widget.
    /// </summary>
    public async Task<bool> EnsureDesktopWidgetRuleAsync(string appId, CancellationToken cancellationToken = default)
    {
        if (!IsKdePlasma)
            return false;

        try
        {
            var entries = RuleEntries(appId);
            if (await IsUpToDateAsync(entries, cancellationToken))
                return true;

            // Plasma 6.8 keeps the rule list in General/Order; older releases used rules + count.
            var legacyRules = await ReadAsync("General", "rules", cancellationToken);
            var order = await ReadAsync("General", "Order", cancellationToken);
            var useLegacy = order.Length == 0 && legacyRules.Length > 0;
            var groups = (useLegacy ? legacyRules : order)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();

            if (!groups.Contains(RuleGroup))
            {
                groups.Add(RuleGroup);
                if (useLegacy)
                {
                    await WriteAsync("General", "rules", string.Join(',', groups), cancellationToken);
                    await WriteAsync("General", "count", groups.Count.ToString(), cancellationToken);
                }
                else
                {
                    await WriteAsync("General", "Order", string.Join(',', groups), cancellationToken);
                }
            }

            foreach (var entry in entries)
                await WriteAsync(RuleGroup, entry.Key, entry.Value, cancellationToken);

            // Drop keys written by earlier builds. Position is handled by KWinWidgetPlacer,
            // because KWin ignores a position rule for this window.
            foreach (var obsolete in ObsoleteKeys)
                await DeleteAsync(RuleGroup, obsolete, cancellationToken);

            await ReconfigureKWinAsync();

            // Give KWin a moment to load the rules before the widget appears.
            await Task.Delay(ReloadSettleTime, cancellationToken);
            logger.LogInformation("KWin rule {Rule} installed for app id {AppId}", RuleGroup, appId);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not install the KWin rule; the widget will stack like a normal window");
            return false;
        }
    }

    private static IReadOnlyList<RuleEntry> RuleEntries(string appId) =>
    [
        new("Description", "LifeCharter desktop widget (keep below, no taskbar)"),
        new("wmclass", appId),
        new("wmclassmatch", ExactMatch),
        new("wmclasscomplete", "false"),
        new("below", "true"),
        new("belowrule", Force),
        new("skiptaskbar", "true"),
        new("skiptaskbarrule", Force),
        new("skippager", "true"),
        new("skippagerrule", Force),
        new("skipswitcher", "true"),
        new("skipswitcherrule", Force),
        new("decorationpolicy", "none"),
        new("decorationpolicyrule", Force),

        // KWin's placement policy (often Centered) is not needed: KWinWidgetPlacer moves the widget.
        new("placement", NoPlacement),
        new("placementrule", Force),
        new("desktops", "\\0"),
        new("desktopsrule", Force),
    ];

    private static async Task<bool> IsUpToDateAsync(IReadOnlyList<RuleEntry> entries, CancellationToken cancellationToken)
    {
        var path = Path.Combine(AppPaths.UserConfigHome, ConfigFile);
        if (!File.Exists(path))
            return false;

        var groups = KConfigFile.Parse(await File.ReadAllLinesAsync(path, cancellationToken).ConfigureAwait(false));
        if (!groups.TryGetValue("General", out var general) || !groups.TryGetValue(RuleGroup, out var rule))
            return false;

        var order = general.GetValueOrDefault("Order") ?? general.GetValueOrDefault("rules") ?? "";
        return order.Split(',', StringSplitOptions.TrimEntries).Contains(RuleGroup) &&
               entries.All(entry => rule.TryGetValue(entry.Key, out var value) && value == entry.Value) &&
               !ObsoleteKeys.Any(rule.ContainsKey);
    }

    private static async Task ReconfigureKWinAsync()
    {
        var address = DBusAddress.Session ?? throw new InvalidOperationException("No D-Bus session bus.");
        using var connection = new DBusConnection(address);
        await connection.ConnectAsync();

        MessageBuffer message;
        using (var writer = connection.GetMessageWriter())
        {
            writer.WriteMethodCallHeader("org.kde.KWin", "/KWin", "org.kde.KWin", "reconfigure", null, MessageFlags.None);
            message = writer.CreateMessage();
        }

        await connection.CallMethodAsync(message);
    }

    private static Task<string> ReadAsync(string group, string key, CancellationToken cancellationToken) =>
        RunAsync("kreadconfig6", ["--file", ConfigFile, "--group", group, "--key", key], cancellationToken);

    private static Task<string> WriteAsync(string group, string key, string value, CancellationToken cancellationToken) =>
        RunAsync("kwriteconfig6", ["--file", ConfigFile, "--group", group, "--key", key, value], cancellationToken);

    private static Task<string> DeleteAsync(string group, string key, CancellationToken cancellationToken) =>
        RunAsync("kwriteconfig6", ["--file", ConfigFile, "--group", group, "--key", key, "--delete"], cancellationToken);

    private static async Task<string> RunAsync(string tool, string[] arguments, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo(tool) { RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException($"Could not start {tool}.");
        var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"{tool} exited with {process.ExitCode}: {await process.StandardError.ReadToEndAsync(cancellationToken)}");

        return output.Trim();
    }

    private readonly record struct RuleEntry(string Key, string Value);
}
