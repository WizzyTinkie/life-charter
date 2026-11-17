using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using LifeCharter.App.Shell.Business;

namespace LifeCharter.App;

public partial class App : Application
{
    private AppShell? shell;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Closing every window leaves the tray running; only Quit ends the app.
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            shell = new AppShell(this, desktop);
            _ = shell.StartAsync(AppCommands.Parse(desktop.Args ?? []));
        }

        base.OnFrameworkInitializationCompleted();
    }
}
