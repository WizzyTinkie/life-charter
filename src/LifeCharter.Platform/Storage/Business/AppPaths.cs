namespace LifeCharter.Platform.Storage.Business;

/// <summary>Per-user folders, following XDG on Linux and the AppData folders on Windows.</summary>
public static class AppPaths
{
    private const string AppName = "LifeCharter";

    /// <summary>Working database. Never synced.</summary>
    public static string DataDirectory { get; } = OperatingSystem.IsWindows()
        ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppName)
        : Path.Combine(XdgDirectory("XDG_DATA_HOME", ".local/share"), AppName);

    /// <summary>Settings and device identity.</summary>
    public static string ConfigDirectory { get; } = OperatingSystem.IsWindows()
        ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppName)
        : Path.Combine(XdgDirectory("XDG_CONFIG_HOME", ".config"), AppName);

    /// <summary>Logs and other state that may be lost.</summary>
    public static string StateDirectory { get; } = OperatingSystem.IsWindows()
        ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppName)
        : Path.Combine(XdgDirectory("XDG_STATE_HOME", ".local/state"), AppName);

    public static string DatabasePath => Path.Combine(DataDirectory, "lifecharter.db");

    private static string XdgDirectory(string variable, string fallback)
    {
        var value = Environment.GetEnvironmentVariable(variable);
        return string.IsNullOrEmpty(value)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), fallback)
            : value;
    }
}
