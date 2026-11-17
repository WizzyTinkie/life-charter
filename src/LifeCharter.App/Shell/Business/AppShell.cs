using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using LifeCharter.App.Logging.Business;
using LifeCharter.App.Shell.Models;
using LifeCharter.App.SkillTree.Views;
using LifeCharter.App.Widget.Views;
using LifeCharter.Platform.AppInstance.Business;
using LifeCharter.Platform.DesktopWidget.Business;
using Microsoft.Extensions.Logging;

namespace LifeCharter.App.Shell.Business;

/// <summary>
/// Owns the long-lived parts of the app: tray icon, desktop widget, single-instance listener.
/// The tree and peek windows are created on demand and dropped when closed, so a closed
/// window holds no GPU surfaces.
/// </summary>
public sealed class AppShell(Application application, IClassicDesktopStyleApplicationLifetime desktop)
{
    private readonly ILogger<AppShell> logger = AppLogging.CreateLogger<AppShell>();
    private TrayIcon? tray;
    private WidgetWindow? widget;
    private PeekWindow? peek;
    private TreeWindow? tree;
    private SingleInstance? instance;

    public async Task StartAsync(AppCommand initial)
    {
        try
        {
            tray = CreateTray();
            TrayIcon.SetIcons(application, [tray]);

            // Install the KWin rule before the widget maps, so it starts below other windows.
            await new KWinRuleInstaller(AppLogging.CreateLogger<KWinRuleInstaller>())
                .EnsureDesktopWidgetRuleAsync(WidgetWindow.AppId);

            widget = new WidgetWindow();
            widget.Show();

            instance = SingleInstance.Listen(OnForwardedCommandAsync, logger);
            Handle(initial);
            logger.LogInformation("Shell started (tray, widget, single-instance listener)");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Shell failed to start");
        }
    }

    public void Handle(AppCommand command)
    {
        switch (command)
        {
            case AppCommand.ShowTree:
                ShowTree();
                break;
            case AppCommand.Peek:
                TogglePeek();
                break;
            case AppCommand.Quit:
                _ = QuitAsync();
                break;
        }
    }

    public async Task QuitAsync()
    {
        logger.LogInformation("Quitting");
        if (instance is not null)
            await instance.DisposeAsync();

        tray?.Dispose();
        desktop.Shutdown();
    }

    private Task OnForwardedCommandAsync(string wire)
    {
        Dispatcher.UIThread.Post(() => Handle(AppCommands.FromWire(wire)));
        return Task.CompletedTask;
    }

    private TrayIcon CreateTray()
    {
        var openTree = new NativeMenuItem("Open tree");
        openTree.Click += (_, _) => ShowTree();
        var peekWidget = new NativeMenuItem("Peek widget");
        peekWidget.Click += (_, _) => TogglePeek();
        var quit = new NativeMenuItem("Quit");
        quit.Click += (_, _) => _ = QuitAsync();

        var icon = new TrayIcon
        {
            Icon = TrayIconFactory.Create(),
            ToolTipText = "LifeCharter",
            Menu = [openTree, peekWidget, new NativeMenuItemSeparator(), quit],
        };
        icon.Clicked += (_, _) => ShowTree();
        return icon;
    }

    private void ShowTree()
    {
        if (tree is null)
        {
            tree = new TreeWindow();
            tree.Closed += (_, _) => tree = null;
            tree.Show();
        }

        tree.Activate();
    }

    private void TogglePeek()
    {
        if (peek is not null)
        {
            peek.Close();
            return;
        }

        peek = new PeekWindow();
        peek.Closed += (_, _) => peek = null;
        peek.Show();
        peek.Activate();
    }
}
