using System.Reflection;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace LifeCharter.Platform.Autostart.Business;

/// <summary>
/// Starts LifeCharter when the user logs in: an XDG autostart entry on Linux (KDE, GNOME…)
/// and the per-user Run key on Windows. Points at the executable that is running now.
/// </summary>
public static class AutostartService
{
    private const string AppName = "LifeCharter";
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public static bool IsSupported => OperatingSystem.IsLinux() || OperatingSystem.IsWindows();

    public static bool IsEnabled()
    {
        if (OperatingSystem.IsWindows())
            return ReadRunValue() is not null;

        return OperatingSystem.IsLinux() && File.Exists(DesktopEntryPath);
    }

    public static async Task SetEnabledAsync(bool enabled, CancellationToken cancellationToken = default)
    {
        if (OperatingSystem.IsWindows())
        {
            WriteRunValue(enabled ? CommandLine() : null);
            return;
        }

        if (!OperatingSystem.IsLinux())
            return;

        if (!enabled)
        {
            File.Delete(DesktopEntryPath);
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(DesktopEntryPath)!);
        var entry = $"""
            [Desktop Entry]
            Type=Application
            Name={AppName}
            Comment=Skill tree for real life
            Exec={CommandLine()}
            Terminal=false
            X-GNOME-Autostart-enabled=true
            """;
        await File.WriteAllTextAsync(DesktopEntryPath, entry + "\n", cancellationToken).ConfigureAwait(false);
    }

    private static string DesktopEntryPath
    {
        get
        {
            var configHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
            if (string.IsNullOrEmpty(configHome))
                configHome = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
            return Path.Combine(configHome, "autostart", "lifecharter.desktop");
        }
    }

    /// <summary>The command that starts this build: the app host, or <c>dotnet app.dll</c> when started through dotnet.</summary>
    private static string CommandLine()
    {
        var process = Environment.ProcessPath ?? throw new InvalidOperationException("Unknown executable path.");
        if (!Path.GetFileNameWithoutExtension(process).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            return Quote(process);

        var entryAssembly = Assembly.GetEntryAssembly()?.Location ?? throw new InvalidOperationException("Unknown entry assembly.");
        return $"{Quote(process)} {Quote(entryAssembly)}";
    }

    private static string Quote(string path) => path.Contains(' ') ? $"\"{path}\"" : path;

    [SupportedOSPlatform("windows")]
    private static string? ReadRunValue()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
        return key?.GetValue(AppName) as string;
    }

    [SupportedOSPlatform("windows")]
    private static void WriteRunValue(string? command)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
        if (command is null)
            key.DeleteValue(AppName, throwOnMissingValue: false);
        else
            key.SetValue(AppName, command);
    }
}
