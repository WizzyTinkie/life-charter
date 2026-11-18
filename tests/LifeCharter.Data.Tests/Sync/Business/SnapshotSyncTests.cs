using LifeCharter.Core.Sync.Models;
using LifeCharter.Data.Sync.Business;
using LifeCharter.Data.Sync.Models;
using Microsoft.Data.Sqlite;

namespace LifeCharter.Data.Tests.Sync.Business;

public sealed class SnapshotSyncTests : IAsyncLifetime
{
    private const string LinuxId = "0193a7-cachyos";
    private const string WindowsId = "0193b2-windows";

    private static readonly SyncedTable[] Tables =
    [
        new("nodes", SyncKind.LastWriterWins, ["id", "title", "updated_at", "deleted_at"]),
        new("log_entries", SyncKind.AppendOnly, ["id", "node_id", "amount"]),
    ];

    private readonly string root = Path.Combine(Path.GetTempPath(), "lifecharter-tests-" + Guid.NewGuid().ToString("N"));
    private Device linux = null!;
    private Device windows = null!;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private string SyncFolder => Path.Combine(root, "Sync", "LifeCharter");

    public async ValueTask InitializeAsync()
    {
        Directory.CreateDirectory(root);
        linux = await Device.CreateAsync(Path.Combine(root, "linux.db"), LinuxId, SyncFolder);
        windows = await Device.CreateAsync(Path.Combine(root, "windows.db"), WindowsId, SyncFolder);
    }

    public async ValueTask DisposeAsync()
    {
        await linux.DisposeAsync();
        await windows.DisposeAsync();
        SqliteConnection.ClearAllPools();
        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public async Task Offline_edits_on_both_devices_all_survive_and_converge()
    {
        await linux.UpsertNodeAsync("n1", "Running", Stamp(100, LinuxId));
        await linux.AddLogAsync("l1", "n1", 5);
        await linux.SyncAsync(Stamp(110, LinuxId));
        await windows.SyncAsync(Stamp(120, WindowsId));
        Assert.Equal("Running", await windows.NodeTitleAsync("n1"));

        // Both machines edit while the other is offline.
        await linux.UpsertNodeAsync("n1", "Running 5k", Stamp(200, LinuxId));
        await linux.AddLogAsync("l2", "n1", 3);
        await windows.UpsertNodeAsync("n1", "Running 10k", Stamp(300, WindowsId));
        await windows.UpsertNodeAsync("n2", "German B2", Stamp(310, WindowsId));
        await windows.AddLogAsync("l3", "n2", 30);

        await linux.ExportAsync(Stamp(400, LinuxId));
        await windows.ExportAsync(Stamp(410, WindowsId));
        await linux.MergeAsync();
        await windows.MergeAsync();

        Assert.Equal("Running 10k", await linux.NodeTitleAsync("n1"));
        Assert.Equal("Running 10k", await windows.NodeTitleAsync("n1"));
        Assert.Equal(["l1", "l2", "l3"], await linux.LogIdsAsync());
        Assert.Equal(await linux.DumpAsync(), await windows.DumpAsync());
    }

    [Fact]
    public async Task Older_remote_edit_does_not_overwrite_newer_local_edit()
    {
        await windows.UpsertNodeAsync("n1", "Old title", Stamp(100, WindowsId));
        await windows.ExportAsync(Stamp(101, WindowsId));
        await linux.UpsertNodeAsync("n1", "New title", Stamp(200, LinuxId));

        await linux.MergeAsync();

        Assert.Equal("New title", await linux.NodeTitleAsync("n1"));
    }

    [Fact]
    public async Task Tombstones_propagate()
    {
        await linux.UpsertNodeAsync("n1", "Smoking-free", Stamp(100, LinuxId));
        await linux.SyncAsync(Stamp(101, LinuxId));
        await windows.SyncAsync(Stamp(102, WindowsId));

        await windows.DeleteNodeAsync("n1", Stamp(200, WindowsId));
        await windows.SyncAsync(Stamp(201, WindowsId));
        await linux.SyncAsync(Stamp(202, LinuxId));

        Assert.True(await linux.IsDeletedAsync("n1"));
    }

    [Fact]
    public async Task Merging_is_idempotent_and_skips_unchanged_snapshots()
    {
        await windows.UpsertNodeAsync("n1", "Sleep", Stamp(100, WindowsId));
        await windows.AddLogAsync("l1", "n1", 8);
        await windows.ExportAsync(Stamp(101, WindowsId));

        var first = await linux.MergeAsync();
        var before = await linux.DumpAsync();
        var second = await linux.MergeAsync();

        Assert.Equal([WindowsId], first.MergedDevices);
        Assert.Empty(second.MergedDevices);
        Assert.Equal(before, await linux.DumpAsync());
        Assert.Equal(Stamp(100, WindowsId), first.MaxRemoteStamp);
    }

    [Fact]
    public async Task Export_is_atomic_and_leaves_a_self_contained_file()
    {
        await linux.UpsertNodeAsync("n1", "Hydration", Stamp(100, LinuxId));
        var path = await linux.ExportAsync(Stamp(101, LinuxId));

        Assert.True(File.Exists(path));
        Assert.False(File.Exists(path + ".tmp"));
        Assert.False(File.Exists(path + "-wal"));

        await using var snapshot = new SqliteConnection($"Data Source={path};Mode=ReadOnly;Pooling=False");
        await snapshot.OpenAsync(Cancellation);
        Assert.Equal("delete", await ScalarAsync(snapshot, "PRAGMA journal_mode"));
        Assert.Equal(LinuxId, await ScalarAsync(snapshot, "SELECT value FROM sync_meta WHERE key = 'device_id'"));
        Assert.Null(await ScalarAsync(snapshot, "SELECT name FROM sqlite_master WHERE name = 'sync_state'"));
    }

    [Fact]
    public async Task Snapshot_from_an_older_schema_still_merges()
    {
        // This device has a newer schema: log_entries gained a "note" column.
        SyncedTable[] newerTables =
        [
            Tables[0],
            new("log_entries", SyncKind.AppendOnly, ["id", "node_id", "amount", "note"]),
        ];
        await using var newer = await Device.CreateAsync(
            Path.Combine(root, "newer.db"), "0193c3-newer", SyncFolder, newerTables, "ALTER TABLE log_entries ADD COLUMN note TEXT;");

        await windows.AddLogAsync("l1", "n1", 2);
        await windows.ExportAsync(Stamp(100, WindowsId));
        await newer.MergeAsync();

        Assert.Equal(["l1"], await newer.LogIdsAsync());
    }

    [Fact]
    public void Merge_sql_for_last_writer_wins_compares_updated_at()
    {
        var sql = SnapshotSync.BuildMergeSql(Tables[0]);

        Assert.Contains("ON CONFLICT(\"id\") DO UPDATE", sql);
        Assert.Contains("WHERE excluded.\"updated_at\" > \"nodes\".\"updated_at\"", sql);
    }

    private static HlcStamp Stamp(long ms, string device) => new(ms, 0, device);

    private static async Task<object?> ScalarAsync(SqliteConnection connection, string sql)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        var value = await cmd.ExecuteScalarAsync(Cancellation);
        return value is DBNull ? null : value;
    }

    private sealed class Device : IAsyncDisposable
    {
        private readonly SqliteConnection db;
        private readonly SnapshotSync sync;

        private Device(SqliteConnection db, SnapshotSync sync)
        {
            this.db = db;
            this.sync = sync;
        }

        public static async Task<Device> CreateAsync(
            string dbPath,
            string deviceId,
            string syncFolder,
            IReadOnlyList<SyncedTable>? tables = null,
            string extraSchema = "")
        {
            var db = new SqliteConnection($"Data Source={dbPath};Pooling=False");
            await db.OpenAsync(Cancellation);
            var device = new Device(db, new SnapshotSync(syncFolder, deviceId, tables ?? Tables));
            await device.ExecuteAsync("""
                PRAGMA journal_mode = WAL;
                CREATE TABLE nodes (id TEXT PRIMARY KEY, title TEXT NOT NULL, updated_at TEXT NOT NULL, deleted_at TEXT);
                CREATE TABLE log_entries (id TEXT PRIMARY KEY, node_id TEXT NOT NULL, amount REAL NOT NULL);
                """ + extraSchema);
            return device;
        }

        public Task UpsertNodeAsync(string id, string title, HlcStamp at) => ExecuteAsync(
            "INSERT INTO nodes (id, title, updated_at) VALUES ($id, $title, $at) " +
            "ON CONFLICT(id) DO UPDATE SET title = excluded.title, updated_at = excluded.updated_at",
            new SqliteParameter("$id", id), new SqliteParameter("$title", title), new SqliteParameter("$at", at.ToString()));

        public Task DeleteNodeAsync(string id, HlcStamp at) => ExecuteAsync(
            "UPDATE nodes SET deleted_at = $at, updated_at = $at WHERE id = $id",
            new SqliteParameter("$id", id), new SqliteParameter("$at", at.ToString()));

        public Task AddLogAsync(string id, string nodeId, double amount) => ExecuteAsync(
            "INSERT INTO log_entries (id, node_id, amount) VALUES ($id, $node, $amount)",
            new SqliteParameter("$id", id), new SqliteParameter("$node", nodeId), new SqliteParameter("$amount", amount));

        public Task<string> ExportAsync(HlcStamp at) => sync.ExportAsync(db, at, Cancellation);

        public Task<MergeReport> MergeAsync() => sync.MergeRemoteSnapshotsAsync(db, Cancellation);

        public async Task SyncAsync(HlcStamp at)
        {
            await MergeAsync();
            await ExportAsync(at);
        }

        public async Task<string?> NodeTitleAsync(string id) =>
            (await QueryAsync("SELECT title FROM nodes WHERE id = $id", new SqliteParameter("$id", id))).SingleOrDefault();

        public async Task<bool> IsDeletedAsync(string id) =>
            (await QueryAsync("SELECT deleted_at FROM nodes WHERE id = $id", new SqliteParameter("$id", id))).Single() is not null;

        public Task<List<string?>> LogIdsAsync() => QueryAsync("SELECT id FROM log_entries ORDER BY id");

        public async Task<List<string?>> DumpAsync() =>
        [
            .. await QueryAsync("SELECT id || '|' || title || '|' || updated_at || '|' || IFNULL(deleted_at, '') FROM nodes ORDER BY id"),
            .. await QueryAsync("SELECT id || '|' || node_id || '|' || amount FROM log_entries ORDER BY id"),
        ];

        public ValueTask DisposeAsync() => db.DisposeAsync();

        private async Task ExecuteAsync(string sql, params SqliteParameter[] parameters)
        {
            await using var cmd = db.CreateCommand();
            cmd.CommandText = sql;
            cmd.Parameters.AddRange(parameters);
            await cmd.ExecuteNonQueryAsync(Cancellation);
        }

        private async Task<List<string?>> QueryAsync(string sql, params SqliteParameter[] parameters)
        {
            await using var cmd = db.CreateCommand();
            cmd.CommandText = sql;
            cmd.Parameters.AddRange(parameters);
            await using var reader = await cmd.ExecuteReaderAsync(Cancellation);
            var rows = new List<string?>();
            while (await reader.ReadAsync(Cancellation))
                rows.Add(reader.IsDBNull(0) ? null : reader.GetValue(0).ToString());
            return rows;
        }
    }
}
