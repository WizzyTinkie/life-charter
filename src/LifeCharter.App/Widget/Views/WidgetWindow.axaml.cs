using System.Runtime.Versioning;
using Avalonia.Controls;
using Avalonia.Input;
using LifeCharter.App.Widget.ViewModels;
using LifeCharter.Platform.DesktopWidget.Business;

namespace LifeCharter.App.Widget.Views;

/// <summary>
/// The desktop widget: frameless, transparent and kept under all other windows. On KDE a
/// KWin rule matched by its own Wayland app id (<see cref="AppId"/>) does the stacking;
/// on Windows the window pins itself to the bottom of the z-order.
/// </summary>
public partial class WidgetWindow : Window
{
    public const string AppId = "lifecharter-widget";

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

    private void OnDragHandlePressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
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
