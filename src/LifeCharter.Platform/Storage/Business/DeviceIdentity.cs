using System.Text.Json;
using System.Text.RegularExpressions;

namespace LifeCharter.Platform.Storage.Business;

/// <summary>
/// A stable id for this installation, used as the HLC device id and the snapshot file name.
/// Stored in the config folder, never in the synced folder.
/// </summary>
public static partial class DeviceIdentity
{
    private const string FileName = "device.json";

    public static async Task<string> LoadOrCreateAsync(string configDirectory, CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(configDirectory, FileName);
        if (File.Exists(path))
        {
            await using var input = File.OpenRead(path);
            var stored = await JsonSerializer.DeserializeAsync<DeviceFile>(input, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(stored?.DeviceId))
                return stored.DeviceId;
        }

        var deviceId = $"{SafeHostName()}-{Guid.CreateVersion7().ToString("N")[^8..]}";
        Directory.CreateDirectory(configDirectory);
        await using (var output = File.Create(path))
            await JsonSerializer.SerializeAsync(output, new DeviceFile(deviceId), cancellationToken: cancellationToken).ConfigureAwait(false);

        return deviceId;
    }

    private static string SafeHostName()
    {
        var name = UnsafeCharacters().Replace(Environment.MachineName.ToLowerInvariant(), "-").Trim('-');
        return name.Length == 0 ? "device" : name;
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex UnsafeCharacters();

    private sealed record DeviceFile(string DeviceId);
}
