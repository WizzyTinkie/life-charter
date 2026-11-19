using Avalonia.Threading;
using LifeCharter.App.Logging.Business;
using LifeCharter.App.Shell.Models;
using LifeCharter.Data.Storage.Business;
using LifeCharter.Data.Sync.Business;
using LifeCharter.Platform.Storage.Business;
using Microsoft.Extensions.Logging;

namespace LifeCharter.App.Sync.Business;

/// <summary>
/// Connects the snapshot sync to the app: starts it for the configured folder, exports after
/// local changes, reloads the chart after remote changes, and writes the daily backup.
/// </summary>
public sealed class SyncController : IAsyncDisposable
{
    private readonly AppServices services;
    private readonly ILogger<SyncController> logger = AppLogging.CreateLogger<SyncController>();

    public SyncController(AppServices services)
    {
        this.services = services;
        services.Session.Changed += (_, _) => Service?.ScheduleExport();
    }

    public SyncService? Service { get; private set; }

    /// <summary>Raised on the UI thread whenever the sync status may have changed.</summary>
    public event EventHandler? StatusChanged;

    /// <summary>Stops the current sync and starts one for <paramref name="folder"/> (null or empty: off).</summary>
    public async Task UseFolderAsync(string? folder)
    {
        if (Service is { } old)
        {
            Service = null;
            await old.DisposeAsync();
        }

        if (!string.IsNullOrWhiteSpace(folder))
        {
            var service = new SyncService(services.ContextFactory, folder, services.DeviceId, AppLogging.CreateLogger<SyncService>());
            service.RemoteChangesMerged += (_, _) => Dispatcher.UIThread.Post(async () => await ReloadAsync());
            service.StatusChanged += (_, _) => Dispatcher.UIThread.Post(() => StatusChanged?.Invoke(this, EventArgs.Empty));
            Service = service;
            try
            {
                await service.StartAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Could not start sync in {Folder}", folder);
            }
        }

        StatusChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task BackupAsync()
    {
        try
        {
            if (await DatabaseBackup.CreateDailyAsync(services.ContextFactory.ConnectionString, AppPaths.BackupDirectory, services.Session.Today) is { } path)
                logger.LogInformation("Daily backup written to {Path}", path);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Daily backup failed");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Service is not null)
            await Service.DisposeAsync();
    }

    private async Task ReloadAsync()
    {
        try
        {
            await services.Session.ReloadAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Reload after sync failed");
        }
    }
}
