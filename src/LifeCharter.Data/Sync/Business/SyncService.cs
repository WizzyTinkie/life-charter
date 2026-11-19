using LifeCharter.Data.Storage.Business;
using LifeCharter.Data.Sync.Models;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace LifeCharter.Data.Sync.Business;

/// <summary>
/// Runs the snapshot sync (PLAN.md section 9) in the background. Exports this device's snapshot
/// a while after the last change and on demand, and merges other devices' snapshots when
/// Syncthing delivers them. An export is skipped when nothing changed since the last one, so
/// two devices never bounce snapshots back and forth.
/// </summary>
public sealed class SyncService : IAsyncDisposable
{
    public static readonly TimeSpan DefaultExportDelay = TimeSpan.FromSeconds(30);

    // Syncthing writes a temp file and renames it; wait for the burst of file events to settle.
    private static readonly TimeSpan MergeDelay = TimeSpan.FromSeconds(2);

    private readonly LifeCharterDbContextFactory factory;
    private readonly SnapshotSync sync;
    private readonly IReadOnlyList<SyncedTable> tables;
    private readonly ILogger<SyncService> logger;
    private readonly TimeSpan exportDelay;
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly Timer exportTimer;
    private readonly Timer mergeTimer;
    private FileSystemWatcher? watcher;
    private string? exportedSignature;
    private bool disposed;

    public SyncService(LifeCharterDbContextFactory factory, string syncFolder, string deviceId, ILogger<SyncService> logger, TimeSpan? exportDelay = null)
    {
        this.factory = factory;
        this.logger = logger;
        this.exportDelay = exportDelay ?? DefaultExportDelay;
        SyncFolder = syncFolder;
        using (var context = factory.CreateDbContext())
            tables = SyncSchemaBuilder.FromModel(context.Model);

        sync = new SnapshotSync(syncFolder, deviceId, tables);
        exportTimer = new Timer(_ => _ = RunAsync(ExportNowAsync, "export"));
        mergeTimer = new Timer(_ => _ = RunAsync(MergeNowAsync, "merge"));
    }

    public string SyncFolder { get; }

    public DateTimeOffset? LastExport { get; private set; }

    public DateTimeOffset? LastMerge { get; private set; }

    public string? LastError { get; private set; }

    /// <summary>Raised (on a worker thread) when a merge changed local rows; reload what you show.</summary>
    public event EventHandler<MergeReport>? RemoteChangesMerged;

    /// <summary>Raised (on a worker thread) after every export, merge or failure.</summary>
    public event EventHandler? StatusChanged;

    /// <summary>Other devices that have a snapshot in the folder.</summary>
    public IReadOnlyList<string> OtherDevices() =>
        Directory.Exists(sync.DevicesFolder)
            ? Directory.EnumerateFiles(sync.DevicesFolder, "*.db")
                .Select(Path.GetFileNameWithoutExtension)
                .OfType<string>()
                .Where(device => sync.OwnSnapshotPath != Path.Combine(sync.DevicesFolder, device + ".db"))
                .Order(StringComparer.Ordinal)
                .ToList()
            : [];

    /// <summary>Merges what is already there, exports this device's snapshot and starts watching the folder.</summary>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(sync.DevicesFolder);
        await EnsureIgnoreFileAsync(cancellationToken).ConfigureAwait(false);
        await MergeNowAsync(cancellationToken).ConfigureAwait(false);
        await ExportNowAsync(cancellationToken).ConfigureAwait(false);

        watcher = new FileSystemWatcher(sync.DevicesFolder, "*.db")
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
        };
        watcher.Created += OnSnapshotEvent;
        watcher.Changed += OnSnapshotEvent;
        watcher.Renamed += OnSnapshotEvent;
        watcher.EnableRaisingEvents = true;
        logger.LogInformation("Sync started in {Folder}", SyncFolder);
    }

    /// <summary>Exports after the export delay; each call restarts the delay.</summary>
    public void ScheduleExport()
    {
        if (!disposed)
            exportTimer.Change(exportDelay, Timeout.InfiniteTimeSpan);
    }

    public async Task<MergeReport> MergeNowAsync(CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        MergeReport report;
        try
        {
            await using var connection = new SqliteConnection(factory.ConnectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            report = await sync.MergeRemoteSnapshotsAsync(connection, cancellationToken).ConfigureAwait(false);
            if (report.MaxRemoteStamp is { } stamp)
                factory.Clock.Observe(stamp);

            LastMerge = DateTimeOffset.Now;
            LastError = null;
        }
        finally
        {
            gate.Release();
        }

        if (report.RowsChanged > 0)
        {
            logger.LogInformation("Merged {Rows} rows from {Devices}", report.RowsChanged, string.Join(", ", report.MergedDevices));
            RemoteChangesMerged?.Invoke(this, report);

            // Pass the merged rows on, so a third device also gets them through this snapshot.
            ScheduleExport();
        }

        StatusChanged?.Invoke(this, EventArgs.Empty);
        return report;
    }

    /// <returns><c>false</c> when nothing changed since the last export.</returns>
    public async Task<bool> ExportNowAsync(CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using var connection = new SqliteConnection(factory.ConnectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            var signature = await SignatureAsync(connection, cancellationToken).ConfigureAwait(false);
            if (signature == exportedSignature && File.Exists(sync.OwnSnapshotPath))
                return false;

            await sync.ExportAsync(connection, factory.Clock.Now(), cancellationToken).ConfigureAwait(false);
            exportedSignature = signature;
            LastExport = DateTimeOffset.Now;
            LastError = null;
            logger.LogInformation("Exported snapshot {Path}", sync.OwnSnapshotPath);
            return true;
        }
        finally
        {
            gate.Release();
            StatusChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Stops watching and writes a last snapshot if anything changed.</summary>
    public async ValueTask DisposeAsync()
    {
        if (disposed)
            return;

        disposed = true;
        watcher?.Dispose();
        await exportTimer.DisposeAsync().ConfigureAwait(false);
        await mergeTimer.DisposeAsync().ConfigureAwait(false);
        await RunAsync(ExportNowAsync, "final export").ConfigureAwait(false);
        gate.Dispose();
    }

    private void OnSnapshotEvent(object sender, FileSystemEventArgs e)
    {
        if (!disposed && e.FullPath != sync.OwnSnapshotPath)
            mergeTimer.Change(MergeDelay, Timeout.InfiniteTimeSpan);
    }

    private async Task RunAsync(Func<CancellationToken, Task> operation, string name)
    {
        try
        {
            await operation(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            logger.LogWarning(ex, "Sync {Operation} failed", name);
            StatusChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Row count and newest stamp of every synced table: changes whenever any synced row does.</summary>
    private async Task<string> SignatureAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        var parts = new List<string>(tables.Count);
        foreach (var table in tables)
        {
            var stampColumn = table.Kind == SyncKind.LastWriterWins ? SyncedTable.UpdatedAtColumn : SyncedTable.CreatedAtColumn;
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = $"SELECT count(*) || ':' || ifnull(max(\"{stampColumn}\"), '') FROM \"{table.Name}\"";
            parts.Add($"{table.Name}={await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false)}");
        }

        return string.Join(';', parts);
    }

    /// <summary>Tells Syncthing to skip half-written snapshots.</summary>
    private async Task EnsureIgnoreFileAsync(CancellationToken cancellationToken)
    {
        var path = Path.Combine(SyncFolder, ".stignore");
        if (!File.Exists(path))
            await File.WriteAllTextAsync(path, "*.tmp\n", cancellationToken).ConfigureAwait(false);
    }
}
