namespace LifeCharter.Platform.DesktopWidget.Models;

/// <summary>
/// Top-left corner of the widget. On KDE Wayland in KWin's logical coordinates, elsewhere in
/// screen pixels; each device only reads back what it saved itself.
/// </summary>
public sealed record WidgetPosition(double X, double Y);
