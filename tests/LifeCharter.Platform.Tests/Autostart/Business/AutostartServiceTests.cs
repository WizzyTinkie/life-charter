using LifeCharter.Platform.Autostart.Business;

namespace LifeCharter.Platform.Tests.Autostart.Business;

public sealed class AutostartServiceTests : IDisposable
{
    private readonly string configHome = Path.Combine(Path.GetTempPath(), "lifecharter-autostart-" + Guid.NewGuid().ToString("N"));
    private readonly string? previousConfigHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");

    public AutostartServiceTests() => Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", configHome);

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Enabling_writes_an_xdg_autostart_entry_for_this_executable()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "XDG autostart is Linux only.");

        await AutostartService.SetEnabledAsync(true, Cancellation);

        var entry = await File.ReadAllTextAsync(Path.Combine(configHome, "autostart", "lifecharter.desktop"), Cancellation);
        Assert.True(AutostartService.IsEnabled());
        Assert.Contains("[Desktop Entry]", entry);
        Assert.Contains("Type=Application", entry);
        Assert.Contains("Exec=", entry);
        Assert.Contains(Path.GetFileName(Environment.ProcessPath!), entry);
    }

    [Fact]
    public async Task Disabling_removes_the_entry()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "XDG autostart is Linux only.");

        await AutostartService.SetEnabledAsync(true, Cancellation);
        await AutostartService.SetEnabledAsync(false, Cancellation);

        Assert.False(AutostartService.IsEnabled());
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", previousConfigHome);
        if (Directory.Exists(configHome))
            Directory.Delete(configHome, recursive: true);
    }
}
