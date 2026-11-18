using System.Text.Json;
using System.Text.Json.Serialization;
using LifeCharter.App.Settings.Models;
using LifeCharter.Platform.Storage.Business;

namespace LifeCharter.App.Settings.Business;

/// <summary>Reads and writes <c>settings.json</c> in the config folder.</summary>
public static class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private static string FilePath => Path.Combine(AppPaths.ConfigDirectory, "settings.json");

    public static async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(FilePath))
            return new AppSettings();

        await using var input = File.OpenRead(FilePath);
        return await JsonSerializer.DeserializeAsync<AppSettings>(input, JsonOptions, cancellationToken) ?? new AppSettings();
    }

    public static async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(AppPaths.ConfigDirectory);
        var temp = FilePath + ".tmp";
        await using (var output = File.Create(temp))
            await JsonSerializer.SerializeAsync(output, settings, JsonOptions, cancellationToken);

        File.Move(temp, FilePath, overwrite: true);
    }
}
