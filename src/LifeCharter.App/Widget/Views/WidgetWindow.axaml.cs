using System.Runtime.Versioning;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using LifeCharter.App.Widget.ViewModels;
using LifeCharter.Platform.DesktopWidget.Business;
using LifeCharter.Platform.DesktopWidget.Models;

namespace LifeCharter.App.Widget.Views;

/// <summary>
/// The desktop widget: frameless, transparent and kept under all other windows. On KDE a
/// KWin rule matched by its own Wayland app id (<see cref="AppId"/>) does the stacking;
/// on Windows the window pins itself to the bottom of the z-order.
/// </summary>
public partial class WidgetWindow : Window
{
    public const string AppId = "lifecharter-widget";

    private static readonly TimeSpan FrameNudgeInterval = TimeSpan.FromMilliseconds(120);

    // Used by the IDE previewer.
    public WidgetWindow()
    {
        InitializeComponent();
        WaylandProperties.SetAppId(this, AppId);
        Body.DragHandle.PointerPressed += OnDragHandlePressed;

        Opened += (_, _) =>
        {
            if (OperatingSystem.IsWindows())
                PinToDesktop();
        };
    }

    public WidgetWindow(WidgetViewModel viewModel) : this() => DataContext = viewModel;

    /// <summary>Raised when the user starts dragging the widget by its title row.</summary>
    public event EventHandler? MoveDragStarted;

    /// <summary>The window position in screen pixels, where the platform reports it (Windows, X11).</summary>
    public WidgetPosition CurrentPosition => new(Position.X, Position.Y);

    /// <summary>Moves the window to a saved position, kept inside its screen's work area (Windows, X11).</summary>
    public void MoveTo(WidgetPosition position)
    {
        var screen = Screens.ScreenFromWindow(this) ?? Screens.Primary;
        if (screen is null)
            return;

        var area = screen.WorkingArea;
        var width = (int)Math.Ceiling(Bounds.Width * screen.Scaling);
        var height = (int)Math.Ceiling(Bounds.Height * screen.Scaling);
        Position = new PixelPoint(
            Math.Clamp((int)Math.Round(position.X), area.X, Math.Max(area.X, area.Right - width)),
            Math.Clamp((int)Math.Round(position.Y), area.Y, Math.Max(area.Y, area.Bottom - height)));
    }

    /// <summary>
    /// Moves the window into a corner of its screen's work area. Works where clients may
    /// position themselves (Windows, X11); on KDE Wayland <see cref="KWinWidgetPlacer"/> does it.
    /// </summary>
    public void MoveToCorner(WidgetCorner corner, int margin)
    {
        var screen = Screens.ScreenFromWindow(this) ?? Screens.Primary;
        if (screen is null)
            return;

        var area = screen.WorkingArea;
        var width = (int)Math.Ceiling(Bounds.Width * screen.Scaling);
        var height = (int)Math.Ceiling(Bounds.Height * screen.Scaling);
        var gap = (int)Math.Round(margin * screen.Scaling);
        var left = corner is WidgetCorner.TopLeft or WidgetCorner.BottomLeft;
        var top = corner is WidgetCorner.TopLeft or WidgetCorner.TopRight;

        Position = new PixelPoint(
            left ? area.X + gap : area.Right - width - gap,
            top ? area.Y + gap : area.Bottom - height - gap);
    }

    /// <summary>
    /// Makes the widget commit a few new frames. On Wayland the compositor applies a pending
    /// move (and decoration change) only when the client acknowledges it with a new frame,
    /// and the idle widget otherwise never draws one.
    /// </summary>
    public async Task CommitFramesAsync()
    {
        for (var i = 0; i < 3; i++)
        {
            await Task.Delay(FrameNudgeInterval);
            Opacity = i % 2 == 0 ? 0.99 : 1;
        }

        Opacity = 1;
    }

    private void OnDragHandlePressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        BeginMoveDrag(e);
        MoveDragStarted?.Invoke(this, EventArgs.Empty);
    }

    [SupportedOSPlatform("windows")]
    private void PinToDesktop()
    {
        if (TryGetPlatformHandle()?.Handle is not { } handle)
            return;

        DesktopPinning.Pin(handle);
        Win32Properties.AddWndProcHookCallback(this, KeepAtBottom);
    }

    [SupportedOSPlatform("windows")]
    private static nint KeepAtBottom(nint window, uint message, nint wParam, nint lParam, ref bool handled)
    {
        if (message == DesktopPinning.WindowPosChangingMessage)
            DesktopPinning.KeepAtBottom(lParam);

        return 0;
    }
}
