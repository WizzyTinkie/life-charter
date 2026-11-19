using LifeCharter.Core.Sync.Models;
using LifeCharter.Data.Sync.Models;
using Microsoft.Data.Sqlite;

namespace LifeCharter.Data.Sync.Business;

/// <summary>
/// Syncs through a Syncthing-shared folder without ever sharing a live database:
/// each device writes only <c>devices/&lt;deviceId&gt;.db</c> and merges everyone else's
/// snapshot into its own working database row by row.
/// </summary>
public sealed class SnapshotSync(string syncFolder, string deviceId, IReadOnlyList<SyncedTable> tables)
{
    public const string DevicesFolderName = "devices";
    private const string RemoteAlias = "remote";

    public string DevicesFolder { get; } = Path.Combine(syncFolder, DevicesFolderName);

    public string OwnSnapshotPath => Path.Combine(DevicesFolder, deviceId + ".db");

    /// <summary>
    /// Writes a consistent copy of the working database to this device's snapshot file.
    /// The copy is built under a temporary name and renamed into place, so Syncthing never
    /// picks up a half-written file.
    /// </summary>
    public async Task<string> ExportAsync(SqliteConnection local, HlcStamp exportedAt, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(DevicesFolder);
        var tempPath = OwnSnapshotPath + ".tmp";
        File.Delete(tempPath);

        await using (var vacuum = local.CreateCommand())
        {
            vacuum.CommandText = "VACUUM INTO $path";
            vacuum.Parameters.AddWithValue("$path", tempPath);
            await vacuum.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await using (var snapshot = await OpenUnpooledAsync(tempPath, cancellationToken).ConfigureAwait(false))
        {
            await ExecuteAsync(snapshot, """
                PRAGMA journal_mode = DELETE;
                DROP TABLE IF EXISTS sync_state;
                CREATE TABLE IF NOT EXISTS sync_meta (key TEXT PRIMARY KEY, value TEXT NOT NULL);
                """, null, cancellationToken).ConfigureAwait(false);

            await using var meta = snapshot.CreateCommand();
            meta.CommandText = """
                INSERT INTO sync_meta (key, value) VALUES ('device_id', $device), ('exported_at', $at)
                ON CONFLICT(key) DO UPDATE SET value = excluded.value;
                """;
            meta.Parameters.AddWithValue("$device", deviceId);
            meta.Parameters.AddWithValue("$at", exportedAt.ToString());
            await meta.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        File.Move(tempPath, OwnSnapshotPath, overwrite: true);
        return OwnSnapshotPath;
    }

    /// <summary>
    /// Merges every other device's snapshot that changed since the last merge.
    /// Safe to call repeatedly; unchanged snapshots are skipped and re-merging is idempotent.
    /// </summary>
    public async Task<MergeReport> MergeRemoteSnapshotsAsync(SqliteConnection local, CancellationToken cancellationToken = default)
    {
        foreach (var table in tables)
            table.Validate();

        await EnsureSyncStateTableAsync(local, cancellationToken).ConfigureAwait(false);

        var merged = new List<string>();
        HlcStamp? maxStamp = null;
        var rowsChanged = 0;

        if (!Directory.Exists(DevicesFolder))
            return new MergeReport(merged, maxStamp);

        foreach (var path in Directory.EnumerateFiles(DevicesFolder, "*.db"))
        {
            var remoteDevice = Path.GetFileNameWithoutExtension(path);
            if (remoteDevice == deviceId)
                continue;

            var info = new FileInfo(path);
            var fingerprint = new SnapshotFingerprint(info.Length, info.LastWriteTimeUtc.Ticks);
            if (await ReadFingerprintAsync(local, remoteDevice, cancellationToken).ConfigureAwait(false) == fingerprint)
                continue;

            // Copy first: Syncthing may replace the file at any moment, and on Windows an
            // open handle would block it.
            var copy = Path.Combine(Path.GetTempPath(), $"lifecharter-merge-{Guid.NewGuid():N}.db");
            await CopyFileAsync(path, copy, cancellationToken).ConfigureAwait(false);
            try
            {
                var outcome = await MergeOneAsync(local, copy, cancellationToken).ConfigureAwait(false);
                if (outcome.MaxStamp is { } s && (maxStamp is null || s > maxStamp.Value))
                    maxStamp = s;
                rowsChanged += outcome.RowsChanged;
                await WriteFingerprintAsync(local, remoteDevice, fingerprint, cancellationToken).ConfigureAwait(false);
                merged.Add(remoteDevice);
            }
            finally
            {
                File.Delete(copy);
            }
        }

        return new MergeReport(merged, maxStamp, rowsChanged);
    }

    internal static string BuildMergeSql(SyncedTable table)
    {
        var columns = string.Join(", ", table.Columns.Select(Quote));
        var target = $"main.{Quote(table.Name)}";
        var source = $"{RemoteAlias}.{Quote(table.Name)}";

        if (table.Kind == SyncKind.AppendOnly)
            return $"INSERT OR IGNORE INTO {target} ({columns}) SELECT {columns} FROM {source};";

        var assignments = string.Join(", ",
            table.Columns.Where(c => c != SyncedTable.IdColumn).Select(c => $"{Quote(c)} = excluded.{Quote(c)}"));

        // "WHERE true" keeps SQLite from parsing ON CONFLICT as part of the SELECT's join.
        return $"""
            INSERT INTO {target} ({columns}) SELECT {columns} FROM {source} WHERE true
            ON CONFLICT({Quote(SyncedTable.IdColumn)}) DO UPDATE SET {assignments}
            WHERE excluded.{Quote(SyncedTable.UpdatedAtColumn)} > {Quote(table.Name)}.{Quote(SyncedTable.UpdatedAtColumn)};
            """;
    }

    private async Task<MergeOutcome> MergeOneAsync(SqliteConnection local, string snapshotPath, CancellationToken cancellationToken)
    {
        await using (var attach = local.CreateCommand())
        {
            attach.CommandText = $"ATTACH DATABASE $path AS {RemoteAlias}";
            attach.Parameters.AddWithValue("$path", snapshotPath);
            await attach.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        try
        {
            HlcStamp? maxStamp = null;
            await using var tx = (SqliteTransaction)await local.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            var changesBefore = await TotalChangesAsync(local, tx, cancellationToken).ConfigureAwait(false);

            foreach (var table in tables)
            {
                var remoteColumns = await RemoteColumnsAsync(local, table.Name, tx, cancellationToken).ConfigureAwait(false);
                if (remoteColumns.Count == 0)
                    continue;

                // A device on an older or newer schema may lack or add columns; merge the shared ones.
                var shared = table with { Columns = table.Columns.Where(remoteColumns.Contains).ToList() };
                await ExecuteAsync(local, BuildMergeSql(shared), tx, cancellationToken).ConfigureAwait(false);

                if (table.Kind == SyncKind.LastWriterWins)
                {
                    var stamp = await ReadMaxUpdatedAtAsync(local, table.Name, tx, cancellationToken).ConfigureAwait(false);
                    if (stamp is { } s && (maxStamp is null || s > maxStamp.Value))
                        maxStamp = s;
                }
            }

            // Ignored inserts and skipped updates are not counted, so this is the number of rows that really changed.
            var changed = await TotalChangesAsync(local, tx, cancellationToken).ConfigureAwait(false) - changesBefore;
            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new MergeOutcome(maxStamp, (int)changed);
        }
        finally
        {
            // Not cancellable: the attachment must be released even when the merge was cancelled.
            await ExecuteAsync(local, $"DETACH DATABASE {RemoteAlias}", null, CancellationToken.None).ConfigureAwait(false);
        }
    }

    private static async Task<long> TotalChangesAsync(SqliteConnection local, SqliteTransaction tx, CancellationToken cancellationToken)
    {
        await using var cmd = local.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "SELECT total_changes()";
        return (long)(await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) ?? 0L);
    }

    /// <summary>Column names of the remote table; empty when the remote snapshot has no such table.</summary>
    private static async Task<HashSet<string>> RemoteColumnsAsync(SqliteConnection local, string table, SqliteTransaction tx, CancellationToken cancellationToken)
    {
        await using var cmd = local.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = $"SELECT name FROM pragma_table_info($table, '{RemoteAlias}')";
        cmd.Parameters.AddWithValue("$table", table);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var columns = new HashSet<string>(StringComparer.Ordinal);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            columns.Add(reader.GetString(0));
        return columns;
    }

    private static async Task<HlcStamp?> ReadMaxUpdatedAtAsync(SqliteConnection local, string table, SqliteTransaction tx, CancellationToken cancellationToken)
    {
        await using var cmd = local.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = $"SELECT MAX({Quote(SyncedTable.UpdatedAtColumn)}) FROM {RemoteAlias}.{Quote(table)}";
        return await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is string s ? HlcStamp.Parse(s) : null;
    }

    private static Task EnsureSyncStateTableAsync(SqliteConnection local, CancellationToken cancellationToken) =>
        ExecuteAsync(local, """
            CREATE TABLE IF NOT EXISTS sync_state (
                device_id TEXT PRIMARY KEY,
                file_length INTEGER NOT NULL,
                file_mtime_ticks INTEGER NOT NULL
            );
            """, null, cancellationToken);

    private static async Task<SnapshotFingerprint?> ReadFingerprintAsync(SqliteConnection local, string remoteDevice, CancellationToken cancellationToken)
    {
        await using var cmd = local.CreateCommand();
        cmd.CommandText = "SELECT file_length, file_mtime_ticks FROM sync_state WHERE device_id = $id";
        cmd.Parameters.AddWithValue("$id", remoteDevice);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
            ? new SnapshotFingerprint(reader.GetInt64(0), reader.GetInt64(1))
            : null;
    }

    private static async Task WriteFingerprintAsync(SqliteConnection local, string remoteDevice, SnapshotFingerprint fingerprint, CancellationToken cancellationToken)
    {
        await using var cmd = local.CreateCommand();
        cmd.CommandText = """
            INSERT INTO sync_state (device_id, file_length, file_mtime_ticks) VALUES ($id, $len, $ticks)
            ON CONFLICT(device_id) DO UPDATE SET file_length = excluded.file_length, file_mtime_ticks = excluded.file_mtime_ticks;
            """;
        cmd.Parameters.AddWithValue("$id", remoteDevice);
        cmd.Parameters.AddWithValue("$len", fingerprint.Length);
        cmd.Parameters.AddWithValue("$ticks", fingerprint.MtimeTicks);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task CopyFileAsync(string source, string destination, CancellationToken cancellationToken)
    {
        await using var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 81920, useAsync: true);
        await using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        await input.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<SqliteConnection> OpenUnpooledAsync(string path, CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Pooling = false,
        }.ToString());
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        return connection;
    }

    private static async Task ExecuteAsync(SqliteConnection connection, string sql, SqliteTransaction? tx, CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string Quote(string identifier) => "\"" + identifier.Replace("\"", "\"\"") + "\"";

    /// <summary>Size and modification time of a remote snapshot when it was last merged.</summary>
    private readonly record struct SnapshotFingerprint(long Length, long MtimeTicks);

    private readonly record struct MergeOutcome(HlcStamp? MaxStamp, int RowsChanged);
}
