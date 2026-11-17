using Avalonia.Controls;
using LifeCharter.App.Help.ViewModels;

namespace LifeCharter.App.Help.Views;

public partial class HelpWindow : Window
{
    public HelpWindow()
    {
        InitializeComponent();
        DataContext = new HelpViewModel();
    }
}
