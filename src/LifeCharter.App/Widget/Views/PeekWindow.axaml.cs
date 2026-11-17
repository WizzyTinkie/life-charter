using Avalonia.Controls;
using Avalonia.Input;

namespace LifeCharter.App.Widget.Views;

/// <summary>
/// The widget's content shown on top of everything after the peek shortcut. A separate
/// window, because the desktop widget itself cannot leave the bottom layer on Wayland.
/// Esc or clicking elsewhere hides it.
/// </summary>
public partial class PeekWindow : Window
{
    public PeekWindow()
    {
        InitializeComponent();
        Body.DragHandle.PointerPressed += (_, e) => BeginMoveDrag(e);
        Deactivated += (_, _) => Close();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape)
            Close();
    }
}
