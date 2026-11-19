using LifeCharter.Core.Common.Models;
using LifeCharter.Core.Journal.Models;
using LifeCharter.Core.SkillTree.Business;
using LifeCharter.Core.Sync.Business;
using LifeCharter.Data.Storage.Business;
using LifeCharter.Data.Sync.Business;
using LifeCharter.Data.Sync.Models;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;

namespace LifeCharter.Data.Tests.Sync.Business;

public sealed class SyncServiceTests : IAsyncLifetime
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "lifecharter-syncservice-" + Guid.NewGuid().ToString("N"));
    private LifeChartStore linuxStore = null!;
    private LifeChartStore windowsStore = null!;
    private SyncService linux = null!;
    private SyncService windows = null!;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private string SyncFolder => Path.Combine(root, "Sync");

    public async ValueTask InitializeAsync()
    {
        Directory.CreateDirectory(root);
        var linuxDevice = Device("linux-1");
        var windowsDevice = Device("windows-1");
        (linuxStore, linux) = (linuxDevice.Store, linuxDevice.Service);
        (windowsStore, windows) = (windowsDevice.Store, windowsDevice.Service);
        await linuxStore.InitializeAsync(StarterTemplate.Create(new DateOnly(2026, 11, 19)), Cancellation);
        await windowsStore.InitializeAsync(LifeChartData.Empty, Cancellation);
    }

    public async ValueTask DisposeAsync()
    {
        await linux.DisposeAsync();
        await windows.DisposeAsync();
        SqliteConnection.ClearAllPools();
        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public async Task Start_exports_a_snapshot_and_writes_the_ignore_file()
    {
        await linux.StartAsync(Cancellation);

        Assert.True(File.Exists(Path.Combine(SyncFolder, "devices", "linux-1.db")));
        Assert.Equal("*.tmp\n", await File.ReadAllTextAsync(Path.Combine(SyncFolder, ".stignore"), Cancellation));
    }

    [Fact]
    public async Task Unchanged_data_is_not_exported_again()
    {
        await linux.StartAsync(Cancellation);

        Assert.False(await linux.ExportNowAsync(Cancellation));

        var node = (await linuxStore.LoadAsync(Cancellation)).Nodes[1];
        await linuxStore.AddEntryAsync(new LogEntry { NodeId = node.Id, Timestamp = DateTimeOffset.UtcNow }, Cancellation);
        Assert.True(await linux.ExportNowAsync(Cancellation));
    }

    [Fact]
    public async Task Merge_reports_changed_rows_once_and_snapshots_stop_bouncing()
    {
        await linux.StartAsync(Cancellation);

        var first = await windows.MergeNowAsync(Cancellation);
        Assert.True(first.RowsChanged > 0);
        Assert.Equal(17, (await windowsStore.LoadAsync(Cancellation)).Nodes.Count);

        // Windows passes the rows on once; Linux already has them, so it does not export again.
        Assert.True(await windows.ExportNowAsync(Cancellation));
        Assert.Equal(0, (await linux.MergeNowAsync(Cancellation)).RowsChanged);
        Assert.False(await linux.ExportNowAsync(Cancellation));
    }

    [Fact]
    public async Task A_new_snapshot_in_the_folder_is_merged_automatically()
    {
        await windows.StartAsync(Cancellation);
        var merged = new TaskCompletionSource<MergeReport>(TaskCreationOptions.RunContinuationsAsynchronously);
        windows.RemoteChangesMerged += (_, report) => merged.TrySetResult(report);

        await linux.StartAsync(Cancellation);

        var report = await merged.Task.WaitAsync(TimeSpan.FromSeconds(15), Cancellation);
        Assert.Contains("linux-1", report.MergedDevices);
    }

    private TestDevice Device(string deviceId)
    {
        var factory = new LifeCharterDbContextFactory(Path.Combine(root, deviceId + ".db"), new HybridLogicalClock(deviceId));
        return new TestDevice(new LifeChartStore(factory), new SyncService(factory, SyncFolder, deviceId, NullLogger<SyncService>.Instance));
    }

    private sealed record TestDevice(LifeChartStore Store, SyncService Service);
}
