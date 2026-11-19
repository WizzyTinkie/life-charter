using Avalonia.Controls;
using Avalonia.Input;
using LifeCharter.App.QuickEntry.ViewModels;

namespace LifeCharter.App.QuickEntry.Views;

/// <summary>
/// The Spotlight-style quick-entry popup (PLAN.md section 8): type, check the preview, press
/// Enter. Esc or clicking elsewhere closes it; a successful entry closes it too.
/// </summary>
public partial class QuickEntryWindow : Window
{
    // Used by the IDE previewer.
    public QuickEntryWindow()
    {
        InitializeComponent();
        Opened += (_, _) => Input.Focus();
        Deactivated += (_, _) => Close();
    }

    public QuickEntryWindow(QuickEntryViewModel viewModel) : this()
    {
        DataContext = viewModel;
        viewModel.Submitted += OnSubmitted;
        Closed += (_, _) => viewModel.Submitted -= OnSubmitted;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape)
            Close();
    }

    private void OnSubmitted(object? sender, EventArgs e) => Close();
}
