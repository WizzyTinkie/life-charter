using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using LifeCharter.App.Help.Views;
using LifeCharter.App.Logging.Business;
using LifeCharter.App.Shell.Models;
using LifeCharter.App.SkillTree.ViewModels;
using LifeCharter.App.SkillTree.Views;
using LifeCharter.App.Widget.ViewModels;
using LifeCharter.App.Widget.Views;
using LifeCharter.Platform.AppInstance.Business;
using LifeCharter.Platform.DesktopWidget.Business;
using Microsoft.Extensions.Logging;

namespace LifeCharter.App.Shell.Business;

/// <summary>
/// Owns the long-lived parts of the app: services, tray icon, desktop widget, single-instance
/// listener. The tree and peek windows are created on demand and dropped when closed, so a
/// closed window holds no GPU surfaces.
/// </summary>
public sealed class AppShell(Application application, IClassicDesktopStyleApplicationLifetime desktop)
{
    private static readonly TimeSpan DayCheckInterval = TimeSpan.FromMinutes(10);

    private readonly ILogger<AppShell> logger = AppLogging.CreateLogger<AppShell>();
    private AppServices? services;
    private TrayIcon? tray;
    private WidgetViewModel? widgetModel;
    private WidgetWindow? widget;
    private PeekWindow? peek;
    private TreeWindow? tree;
    private HelpWindow? help;
    private SingleInstance? instance;
    private DispatcherTimer? dayTimer;

    public async Task StartAsync(AppCommand initial)
    {
        try
        {
            tray = CreateTray();
            TrayIcon.SetIcons(application, [tray]);

            services = await AppServicesFactory.CreateAsync();
            logger.LogInformation("Chart loaded for device {DeviceId}: {Nodes} nodes", services.DeviceId, services.Session.Chart.Nodes.Count);

            // Install the KWin rule before the widget maps, so it starts below other windows.
            await new KWinRuleInstaller(AppLogging.CreateLogger<KWinRuleInstaller>())
                .EnsureDesktopWidgetRuleAsync(WidgetWindow.AppId);

            widgetModel = new WidgetViewModel(services.Session, ShowTree);
            widget = new WidgetWindow(widgetModel);
            widget.Show();

            StartDayTimer(services);
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
            case AppCommand.Help:
                ShowHelp();
                break;
            case AppCommand.Quit:
                _ = QuitAsync();
                break;
        }
    }

    public async Task QuitAsync()
    {
        logger.LogInformation("Quitting");
        dayTimer?.Stop();
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

    /// <summary>Progression depends on the date; recompute once the day changes.</summary>
    private void StartDayTimer(AppServices appServices)
    {
        var lastDay = appServices.Session.Today;
        dayTimer = new DispatcherTimer(DayCheckInterval, DispatcherPriority.Background, async (_, _) =>
        {
            if (appServices.Session.Today == lastDay)
                return;

            lastDay = appServices.Session.Today;
            try
            {
                await appServices.Session.ReloadAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Day rollover reload failed");
            }
        });
        dayTimer.Start();
    }

    private TrayIcon CreateTray()
    {
        var openTree = new NativeMenuItem("Open tree");
        openTree.Click += (_, _) => ShowTree();
        var peekWidget = new NativeMenuItem("Peek widget");
        peekWidget.Click += (_, _) => TogglePeek();
        var helpItem = new NativeMenuItem("Help");
        helpItem.Click += (_, _) => ShowHelp();
        var quit = new NativeMenuItem("Quit");
        quit.Click += (_, _) => _ = QuitAsync();

        var icon = new TrayIcon
        {
            Icon = TrayIconFactory.Create(),
            ToolTipText = "LifeCharter",
            Menu = [openTree, peekWidget, helpItem, new NativeMenuItemSeparator(), quit],
        };
        icon.Clicked += (_, _) => ShowTree();
        return icon;
    }

    private void ShowTree()
    {
        if (services is null)
            return;

        if (tree is null)
        {
            tree = new TreeWindow(new TreeWindowViewModel(services.Session, ShowHelp));
            tree.Closed += (_, _) => tree = null;
            tree.Show();
        }

        tree.Activate();
    }

    private void ShowHelp()
    {
        if (help is null)
        {
            help = new HelpWindow();
            help.Closed += (_, _) => help = null;
            help.Show();
        }

        help.Activate();
    }

    private void TogglePeek()
    {
        if (widgetModel is null)
            return;

        if (peek is not null)
        {
            peek.Close();
            return;
        }

        peek = new PeekWindow(widgetModel);
        peek.Closed += (_, _) => peek = null;
        peek.Show();
        peek.Activate();
    }
}
