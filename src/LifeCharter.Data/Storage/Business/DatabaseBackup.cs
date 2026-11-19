using Microsoft.Data.Sqlite;

namespace LifeCharter.Data.Storage.Business;

/// <summary>A daily rolling copy of the working database (PLAN.md section 9).</summary>
public static class DatabaseBackup
{
    public const int Keep = 14;
    private const string Prefix = "lifecharter-";

    /// <summary>
    /// Writes today's backup with <c>VACUUM INTO</c> unless it exists, then deletes all but the
    /// newest <see cref="Keep"/>. Returns the new file, or null when today's backup was already there.
    /// </summary>
    public static async Task<string?> CreateDailyAsync(string connectionString, string backupFolder, DateOnly today, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(backupFolder);
        var path = Path.Combine(backupFolder, $"{Prefix}{today:yyyy-MM-dd}.db");
        if (File.Exists(path))
            return null;

        var tempPath = path + ".tmp";
        File.Delete(tempPath);
        await using (var connection = new SqliteConnection(connectionString))
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using var vacuum = connection.CreateCommand();
            vacuum.CommandText = "VACUUM INTO $path";
            vacuum.Parameters.AddWithValue("$path", tempPath);
            await vacuum.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        File.Move(tempPath, path);

        // The date in the name sorts chronologically.
        foreach (var old in Directory.EnumerateFiles(backupFolder, Prefix + "*.db").OrderDescending(StringComparer.Ordinal).Skip(Keep))
            File.Delete(old);

        return path;
    }
}
