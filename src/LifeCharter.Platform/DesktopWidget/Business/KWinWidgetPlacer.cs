using System.Globalization;
using LifeCharter.Platform.DesktopWidget.Models;
using LifeCharter.Platform.Storage.Business;
using Microsoft.Extensions.Logging;
using Tmds.DBus.Protocol;

namespace LifeCharter.Platform.DesktopWidget.Business;

/// <summary>
/// Places and locates the desktop widget on KDE Plasma (Wayland), where clients can neither
/// position themselves nor read their position, and KWin ignores a position rule for the
/// widget. Moves go through a one-off KWin script; reading uses KWin's window search and
/// <c>getWindowInfo</c> over D-Bus.
/// </summary>
public sealed class KWinWidgetPlacer(ILogger<KWinWidgetPlacer> logger)
{
    private const string PluginName = "lifecharter-widget-placement";
    private const string KWinService = "org.kde.KWin";

    public Task<bool> PlaceAsync(string appId, WidgetCorner corner, int margin, CancellationToken cancellationToken = default) =>
        RunScriptAsync(BuildCornerScript(appId, corner, margin), $"{corner} corner", cancellationToken);

    public Task<bool> PlaceAtAsync(string appId, WidgetPosition position, CancellationToken cancellationToken = default) =>
        RunScriptAsync(BuildPositionScript(appId, position), $"saved position {position.X:0},{position.Y:0}", cancellationToken);

    /// <summary>Where KWin has the widget now; null when it cannot be found or KDE is not running.</summary>
    public async Task<WidgetPosition?> ReadPositionAsync(string caption, string appId, CancellationToken cancellationToken = default)
    {
        if (!KWinRuleInstaller.IsKdePlasma)
            return null;

        try
        {
            using var connection = await ConnectAsync().ConfigureAwait(false);
            foreach (var match in await FindWindowsAsync(connection, caption).ConfigureAwait(false))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (match.Title != caption)
                    continue;

                var info = await WindowInfoAsync(connection, match.Uuid).ConfigureAwait(false);
                if (info.ResourceClass == appId)
                    return new WidgetPosition(info.X, info.Y);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Could not read the widget position from KWin");
        }

        return null;
    }

    internal static string BuildCornerScript(string appId, WidgetCorner corner, int margin)
    {
        var left = corner is WidgetCorner.TopLeft or WidgetCorner.BottomLeft ? "true" : "false";
        var top = corner is WidgetCorner.TopLeft or WidgetCorner.TopRight ? "true" : "false";
        var gap = margin.ToString(CultureInfo.InvariantCulture);
        return BuildScript(
            appId,
            $"{left} ? area.x + {gap} : area.x + area.width - c.width - {gap}",
            $"{top} ? area.y + {gap} : area.y + area.height - c.height - {gap}");
    }

    /// <summary>The saved position, kept inside the work area in case the screen layout changed.</summary>
    internal static string BuildPositionScript(string appId, WidgetPosition position)
    {
        var x = position.X.ToString("0.##", CultureInfo.InvariantCulture);
        var y = position.Y.ToString("0.##", CultureInfo.InvariantCulture);
        return BuildScript(
            appId,
            $"Math.min(Math.max({x}, area.x), area.x + area.width - c.width)",
            $"Math.min(Math.max({y}, area.y), area.y + area.height - c.height)");
    }

    // If KWin has not mapped the widget yet, the script waits for it: windowAdded fires after
    // KWin's own placement, so the move wins either way. Positions are measured with the client
    // size, because the frame can carry a stale margin until the widget commits a new frame.
    private static string BuildScript(string appId, string xExpression, string yExpression) =>
        $$"""
        function place(w) {
            if (w.resourceClass !== "{{appId}}") return false;
            const area = workspace.clientArea(KWin.PlacementArea, w);
            const g = w.frameGeometry;
            const c = w.clientGeometry;
            w.frameGeometry = {
                x: {{xExpression}},
                y: {{yExpression}},
                width: g.width,
                height: g.height
            };
            return true;
        }

        let placed = false;
        const windows = workspace.windowList();
        for (let i = 0; i < windows.length; i++)
            placed = place(windows[i]) || placed;

        if (!placed) {
            const onAdded = function (w) {
                if (place(w))
                    workspace.windowAdded.disconnect(onAdded);
            };
            workspace.windowAdded.connect(onAdded);
        }
        """;

    private async Task<bool> RunScriptAsync(string script, string description, CancellationToken cancellationToken)
    {
        if (!KWinRuleInstaller.IsKdePlasma)
            return false;

        try
        {
            Directory.CreateDirectory(AppPaths.StateDirectory);
            var scriptPath = Path.Combine(AppPaths.StateDirectory, "kwin-widget-placement.js");
            await File.WriteAllTextAsync(scriptPath, script, cancellationToken).ConfigureAwait(false);

            using var connection = await ConnectAsync().ConfigureAwait(false);

            // A script of the same name from an earlier placement must go first.
            await CallAsync(connection, "/Scripting", "org.kde.kwin.Scripting", "unloadScript", PluginName).ConfigureAwait(false);
            var scriptId = await LoadScriptAsync(connection, scriptPath).ConfigureAwait(false);
            if (scriptId < 0)
                return false;

            await CallAsync(connection, $"/Scripting/Script{scriptId}", "org.kde.kwin.Script", "run", null).ConfigureAwait(false);
            logger.LogInformation("Widget placed at its {Placement}", description);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not place the widget through KWin");
            return false;
        }
    }

    private static async Task<DBusConnection> ConnectAsync()
    {
        var address = DBusAddress.Session ?? throw new InvalidOperationException("No D-Bus session bus.");
        var connection = new DBusConnection(address);
        try
        {
            await connection.ConnectAsync().ConfigureAwait(false);
            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    /// <summary>KWin's window search (the KRunner windows runner) returns ids like <c>0_{uuid}</c>.</summary>
    private static async Task<IReadOnlyList<WindowMatch>> FindWindowsAsync(DBusConnection connection, string query)
    {
        MessageBuffer message;
        using (var writer = connection.GetMessageWriter())
        {
            writer.WriteMethodCallHeader(KWinService, "/WindowsRunner", "org.kde.krunner1", "Match", "s", MessageFlags.None);
            writer.WriteString(query);
            message = writer.CreateMessage();
        }

        return await connection.CallMethodAsync(message, static (reply, _) =>
        {
            var reader = reply.GetBodyReader();
            var matches = new List<WindowMatch>();
            var end = reader.ReadArrayStart(DBusType.Struct);
            while (reader.HasNext(end))
            {
                reader.AlignStruct();
                var id = reader.ReadString();
                var title = reader.ReadString();
                reader.ReadString();
                reader.ReadInt32();
                reader.ReadDouble();
                reader.ReadDictionaryOfStringToVariantValue();

                var separator = id.IndexOf('_');
                if (separator >= 0)
                    matches.Add(new WindowMatch(id[(separator + 1)..], title));
            }

            return matches;
        }, null).ConfigureAwait(false);
    }

    private static async Task<WindowInfo> WindowInfoAsync(DBusConnection connection, string uuid)
    {
        MessageBuffer message;
        using (var writer = connection.GetMessageWriter())
        {
            writer.WriteMethodCallHeader(KWinService, "/KWin", "org.kde.KWin", "getWindowInfo", "s", MessageFlags.None);
            writer.WriteString(uuid);
            message = writer.CreateMessage();
        }

        return await connection.CallMethodAsync(message, static (reply, _) =>
        {
            var values = reply.GetBodyReader().ReadDictionaryOfStringToVariantValue();
            return new WindowInfo(
                values.TryGetValue("resourceClass", out var resourceClass) ? resourceClass.GetString() : "",
                values.TryGetValue("x", out var x) ? x.GetDouble() : 0,
                values.TryGetValue("y", out var y) ? y.GetDouble() : 0);
        }, null).ConfigureAwait(false);
    }

    private static async Task<int> LoadScriptAsync(DBusConnection connection, string scriptPath)
    {
        MessageBuffer message;
        using (var writer = connection.GetMessageWriter())
        {
            writer.WriteMethodCallHeader(KWinService, "/Scripting", "org.kde.kwin.Scripting", "loadScript", "ss", MessageFlags.None);
            writer.WriteString(scriptPath);
            writer.WriteString(PluginName);
            message = writer.CreateMessage();
        }

        return await connection.CallMethodAsync(message, static (reply, _) => reply.GetBodyReader().ReadInt32(), null).ConfigureAwait(false);
    }

    private static async Task CallAsync(DBusConnection connection, string path, string @interface, string member, string? argument)
    {
        MessageBuffer message;
        using (var writer = connection.GetMessageWriter())
        {
            writer.WriteMethodCallHeader(KWinService, path, @interface, member, argument is null ? null : "s", MessageFlags.None);
            if (argument is not null)
                writer.WriteString(argument);
            message = writer.CreateMessage();
        }

        await connection.CallMethodAsync(message).ConfigureAwait(false);
    }

    private sealed record WindowMatch(string Uuid, string Title);

    private sealed record WindowInfo(string ResourceClass, double X, double Y);
}
