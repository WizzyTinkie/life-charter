using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LifeCharter.App.Sync.Business;

namespace LifeCharter.App.Sync.ViewModels;

/// <summary>Sync folder choice and status: last export and merge, the other devices seen, errors.</summary>
public sealed partial class SyncViewModel : ObservableObject, IDisposable
{
    private readonly SyncController controller;
    private readonly Func<string?, Task> saveFolder;

    public SyncViewModel(SyncController controller, string? folder, Func<string?, Task> saveFolder)
    {
        this.controller = controller;
        this.saveFolder = saveFolder;
        Folder = folder ?? DefaultFolder;
        controller.StatusChanged += OnStatusChanged;
        Refresh();
    }

    /// <summary>A suggestion: Syncthing's default folder.</summary>
    public static string DefaultFolder { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Sync", "LifeCharter");

    [ObservableProperty]
    public partial string Folder { get; set; }

    [ObservableProperty]
    public partial bool IsOn { get; set; }

    [ObservableProperty]
    public partial string StatusText { get; set; } = "";

    [ObservableProperty]
    public partial string DevicesText { get; set; } = "";

    [ObservableProperty]
    public partial string ErrorText { get; set; } = "";

    [ObservableProperty]
    public partial bool HasError { get; set; }

    public void Dispose() => controller.StatusChanged -= OnStatusChanged;

    [RelayCommand]
    private Task SaveAsync() => saveFolder(string.IsNullOrWhiteSpace(Folder) ? null : Folder.Trim());

    [RelayCommand]
    private Task TurnOffAsync() => saveFolder(null);

    [RelayCommand]
    private async Task SyncNowAsync()
    {
        if (controller.Service is not { } service)
            return;

        await service.MergeNowAsync();
        await service.ExportNowAsync();
    }

    private void OnStatusChanged(object? sender, EventArgs e) => Refresh();

    private void Refresh()
    {
        var service = controller.Service;
        IsOn = service is not null;
        if (service is null)
        {
            StatusText = "Sync is off. This machine keeps its data to itself.";
            DevicesText = "";
            HasError = false;
            return;
        }

        StatusText = $"Syncing through {service.SyncFolder} · last export {Time(service.LastExport)} · last merge {Time(service.LastMerge)}";
        var others = service.OtherDevices();
        DevicesText = others.Count == 0
            ? "No other device yet. Share this folder with your other machine in Syncthing and turn sync on there too."
            : "Other devices: " + string.Join(", ", others);
        ErrorText = service.LastError ?? "";
        HasError = ErrorText.Length > 0;
    }

    private static string Time(DateTimeOffset? time) =>
        time is { } value ? value.ToLocalTime().ToString("HH:mm:ss", CultureInfo.CurrentCulture) : "never";
}
