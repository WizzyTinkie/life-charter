using Avalonia.Controls;
using LifeCharter.App.Siege.ViewModels;

namespace LifeCharter.App.Siege.Views;

public partial class SiegeSettingsWindow : Window
{
    // Used by the IDE previewer.
    public SiegeSettingsWindow()
    {
        InitializeComponent();
    }

    public SiegeSettingsWindow(SiegeSettingsViewModel viewModel) : this()
    {
        DataContext = viewModel;
        Closed += (_, _) => viewModel.Dispose();
    }
}
