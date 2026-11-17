using System.Diagnostics;
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

    // KWin rule policies: 2 = Force, 4 = Remember. String match: 1 = exact.
    private const string Force = "2";
    private const string Remember = "4";
    private const string ExactMatch = "1";

    public static bool IsKdePlasma =>
        OperatingSystem.IsLinux() &&
        (Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP") ?? "").Contains("KDE", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Installs or refreshes the rule for windows whose Wayland app id (window class) is
    /// <paramref name="appId"/>. The app id is set before the window maps, unlike the title,
    /// so KWin matches it when the window first appears.
    /// </summary>
    public async Task<bool> EnsureDesktopWidgetRuleAsync(string appId, CancellationToken cancellationToken = default)
    {
        if (!IsKdePlasma)
            return false;

        try
        {
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

            (string Key, string Value)[] entries =
            [
                ("Description", "LifeCharter desktop widget (keep below, no taskbar)"),
                ("wmclass", appId),
                ("wmclassmatch", ExactMatch),
                ("wmclasscomplete", "false"),
                ("below", "true"),
                ("belowrule", Force),
                ("skiptaskbar", "true"),
                ("skiptaskbarrule", Force),
                ("skippager", "true"),
                ("skippagerrule", Force),
                ("skipswitcher", "true"),
                ("skipswitcherrule", Force),
                ("decorationpolicy", "none"),
                ("decorationpolicyrule", Force),
                ("desktops", "\\0"),
                ("desktopsrule", Force),
                ("position", "40,60"),
                ("positionrule", Remember),
            ];

            foreach (var (key, value) in entries)
                await WriteAsync(RuleGroup, key, value, cancellationToken);

            // Drop the title match written by earlier spike builds.
            await DeleteAsync(RuleGroup, "title", cancellationToken);
            await DeleteAsync(RuleGroup, "titlematch", cancellationToken);

            await ReconfigureKWinAsync();
            logger.LogInformation("KWin rule {Rule} installed for app id {AppId}", RuleGroup, appId);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not install the KWin rule; the widget will stack like a normal window");
            return false;
        }
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
}
