using LifeCharter.Platform.DesktopWidget.Models;

namespace LifeCharter.App.Settings.Models;

/// <summary>Settings of this device only; never synced (window placement differs per machine).</summary>
public sealed record AppSettings
{
    public WidgetCorner WidgetCorner { get; init; } = WidgetCorner.TopRight;

    /// <summary>Distance from the screen edges, in logical pixels.</summary>
    public int WidgetMargin { get; init; } = 24;

    /// <summary>Where the user last dragged the widget; null keeps it in <see cref="WidgetCorner"/>.</summary>
    public WidgetPosition? WidgetPosition { get; init; }

    /// <summary>The Syncthing-shared folder for device snapshots; null turns sync off.</summary>
    public string? SyncFolder { get; init; }
}
