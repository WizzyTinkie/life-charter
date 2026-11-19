using Avalonia.Controls;
using Avalonia.Platform.Storage;
using LifeCharter.App.Sync.ViewModels;

namespace LifeCharter.App.Sync.Views;

public partial class SyncWindow : Window
{
    // Used by the IDE previewer.
    public SyncWindow()
    {
        InitializeComponent();
        Browse.Click += async (_, _) => await BrowseAsync();
    }

    public SyncWindow(SyncViewModel viewModel) : this()
    {
        DataContext = viewModel;
        Closed += (_, _) => viewModel.Dispose();
    }

    private async Task BrowseAsync()
    {
        if (DataContext is not SyncViewModel viewModel)
            return;

        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "Syncthing folder for LifeCharter" });
        if (folders.Count > 0 && folders[0].TryGetLocalPath() is { } path)
            viewModel.Folder = path;
    }
}
