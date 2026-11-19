using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using LifeCharter.App.Help.Views;
using LifeCharter.App.Logging.Business;
using LifeCharter.App.Notifications.Business;
using LifeCharter.App.QuickEntry.Business;
using LifeCharter.App.QuickEntry.ViewModels;
using LifeCharter.App.QuickEntry.Views;
using LifeCharter.App.Settings.Business;
using LifeCharter.App.Settings.Models;
using LifeCharter.App.Shell.Models;
using LifeCharter.App.Siege.ViewModels;
using LifeCharter.App.Siege.Views;
using LifeCharter.App.SkillTree.ViewModels;
using LifeCharter.App.SkillTree.Views;
using LifeCharter.App.Sync.Business;
using LifeCharter.App.Sync.ViewModels;
using LifeCharter.App.Sync.Views;
using LifeCharter.App.Widget.ViewModels;
using LifeCharter.App.Widget.Views;
using LifeCharter.Core.Journal.Models;
using LifeCharter.Core.QuickEntry.Models;
using LifeCharter.Platform.AppInstance.Business;
using LifeCharter.Platform.Autostart.Business;
using LifeCharter.Platform.DesktopWidget.Business;
using LifeCharter.Platform.DesktopWidget.Models;
using LifeCharter.Platform.Notifications.Business;
using LifeCharter.Platform.Notifications.Interfaces;
using LifeCharter.Platform.Shortcuts.Interfaces;
using Microsoft.Extensions.Logging;

namespace LifeCharter.App.Shell.Business;

/// <summary>
/// Owns the long-lived parts of the app: services, tray icon, desktop widget, single-instance
/// listener, sync, notifications and global shortcuts. Other windows are created on demand and
/// dropped when closed, so a closed window holds no GPU surfaces.
/// </summary>
public sealed class AppShell(Application application, IClassicDesktopStyleApplicationLifetime desktop)
{
    private static readonly TimeSpan DayCheckInterval = TimeSpan.FromMinutes(10);

    // KWin maps the window shortly after Avalonia reports it opened; place it after that.
    private static readonly TimeSpan PlacementDelay = TimeSpan.FromMilliseconds(300);

    // After a drag starts, KWin is polled until the position stops changing (up to 30 s).
    private static readonly TimeSpan DragPollInterval = TimeSpan.FromMilliseconds(400);
    private const int DragPollLimit = 75;

    private readonly ILogger<AppShell> logger = AppLogging.CreateLogger<AppShell>();
    private AppServices? services;
    private AppSettings settings = new();
    private readonly KWinWidgetPlacer placer = new(AppLogging.CreateLogger<KWinWidgetPlacer>());
    private DispatcherTimer? replaceTimer;
    private readonly List<NativeMenuItem> cornerItems = [];
    private bool trackingDrag;
    private bool placing;
    private WidgetPosition? lastPlacedPosition;
    private DispatcherTimer? positionSaveTimer;
    private TrayIcon? tray;
    private WidgetViewModel? widgetModel;
    private WidgetWindow? widget;
    private PeekWindow? peek;
    private TreeWindow? tree;
    private HelpWindow? help;
    private SiegeSettingsWindow? siegeSettings;
    private QuickEntryWindow? quickEntry;
    private SyncWindow? syncWindow;
    private TrayDefenseMenu? defenseMenu;
    private bool trayAlert;
    private SyncController? sync;
    private INotifier? notifier;
    private ThreatReporter? reporter;
    private IGlobalShortcuts? shortcuts;
    private SingleInstance? instance;
    private DispatcherTimer? dayTimer;

    public async Task StartAsync(AppRequest initial)
    {
        try
        {
            settings = await SettingsStore.LoadAsync();
            tray = CreateTray();
            TrayIcon.SetIcons(application, [tray]);

            services = await AppServicesFactory.CreateAsync();
            logger.LogInformation("Chart loaded for device {DeviceId}: {Nodes} nodes", services.DeviceId, services.Session.Chart.Nodes.Count);

            defenseMenu = new TrayDefenseMenu(services.Session);
            tray.Menu?.Items.Insert(3, defenseMenu.Item);
            services.Session.Changed += (_, _) => Dispatcher.UIThread.Post(RefreshTray);
            RefreshTray();

            // Install the KWin rule before the widget maps, so it starts below other windows.
            await new KWinRuleInstaller(AppLogging.CreateLogger<KWinRuleInstaller>())
                .EnsureDesktopWidgetRuleAsync(WidgetWindow.AppId);

            widgetModel = new WidgetViewModel(services.Session, ShowTree);
            widget = new WidgetWindow(widgetModel);
            widget.Opened += (_, _) => SchedulePlacement();
            widget.SizeChanged += (_, e) =>
            {
                // Bottom corners grow upwards, and a saved position may need clamping: re-place
                // when the height changes.
                if (e.HeightChanged && (settings.WidgetPosition is not null ||
                                        settings.WidgetCorner is WidgetCorner.BottomLeft or WidgetCorner.BottomRight))
                    SchedulePlacement();
            };
            widget.MoveDragStarted += (_, _) => _ = TrackDragAsync();
            widget.PositionChanged += (_, _) => SchedulePositionSave();
            widget.Opened += (_, _) => _ = RegisterShortcutsAsync();
            widget.Show();

            StartDayTimer(services);
            instance = SingleInstance.Listen(OnForwardedCommandAsync, logger);
            _ = HandleAsync(initial);
            logger.LogInformation("Shell started (tray, widget, single-instance listener)");

            sync = new SyncController(services);
            await sync.BackupAsync();
            await sync.UseFolderAsync(settings.SyncFolder);

            notifier = OperatingSystem.IsLinux()
                ? new FreedesktopNotifier(AppLogging.CreateLogger<FreedesktopNotifier>())
                : new LoggingNotifier(AppLogging.CreateLogger<LoggingNotifier>());
            reporter = new ThreatReporter(services.Session, notifier, ShowPeek, AppLogging.CreateLogger<ThreatReporter>());
            await reporter.StartAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Shell failed to start");
        }
    }

    /// <returns>The reply for the launch that sent the request (printed by the CLI).</returns>
    public async Task<string> HandleAsync(AppRequest request)
    {
        try
        {
            switch (request.Command)
            {
                case AppCommand.ShowTree:
                    ShowTree();
                    return "";
                case AppCommand.Peek:
                    TogglePeek();
                    return "";
                case AppCommand.Help:
                    ShowHelp();
                    return "";
                case AppCommand.Quick:
                    ShowQuickEntry();
                    return "";
                case AppCommand.Log:
                    return await LogAsync(request.Argument);
                case AppCommand.Status:
                    return services is null ? "LifeCharter is still starting." : StatusReport.Build(services.Session);
                case AppCommand.Quit:
                    _ = QuitAsync();
                    return "Quitting LifeCharter.";
                default:
                    return "";
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Command {Command} failed", request.Command);
            return $"Failed: {ex.Message}";
        }
    }

    public async Task QuitAsync()
    {
        logger.LogInformation("Quitting");
        dayTimer?.Stop();
        await SaveKWinPositionAsync();
        if (instance is not null)
            await instance.DisposeAsync();
        if (shortcuts is not null)
            await shortcuts.DisposeAsync();
        if (notifier is not null)
            await notifier.DisposeAsync();
        if (sync is not null)
            await sync.DisposeAsync();

        tray?.Dispose();
        desktop.Shutdown();
    }

    private Task<string> OnForwardedCommandAsync(string wire) =>
        Dispatcher.UIThread.InvokeAsync(() => HandleAsync(AppCommands.FromWire(wire)));

    /// <summary>A quick-entry line from <c>lifecharter log …</c>.</summary>
    private async Task<string> LogAsync(string text)
    {
        if (services is null)
            return "LifeCharter is still starting.";

        var session = services.Session;
        var draft = session.ParseQuickEntry(text);
        if (draft.Kind == QuickEntryKind.Today)
            return StatusReport.Build(session);
        if (!draft.CanApply)
            return draft.Kind == QuickEntryKind.None ? "Nothing to log. Example: lifecharter log water 2l" : draft.Message;

        var outcome = session.Preview(draft);
        return await session.ApplyAsync(draft, EntrySource.Cli)
            ? QuickEntryText.Done(session, draft, outcome)
            : "Nothing logged today to undo.";
    }

    private async Task RegisterShortcutsAsync()
    {
        if (shortcuts is not null || widget is null || GlobalShortcutSetup.Create(widget) is not { } created)
            return;

        shortcuts = created;
        shortcuts.Pressed += (_, id) => Dispatcher.UIThread.Post(() =>
        {
            if (id == GlobalShortcutSetup.QuickEntry)
                ShowQuickEntry();
            else if (id == GlobalShortcutSetup.Peek)
                TogglePeek();
        });
        await shortcuts.RegisterAsync(GlobalShortcutSetup.Defaults);
    }

    /// <summary>Today's defenses in the tray, and the red badge while attacks wait in the evening.</summary>
    private void RefreshTray()
    {
        if (tray is null || defenseMenu is null)
            return;

        defenseMenu.Refresh();
        var alert = defenseMenu.ShouldAlert(DateTimeOffset.Now);
        if (alert == trayAlert)
            return;

        trayAlert = alert;
        tray.Icon = TrayIconFactory.Create(alert);
        tray.ToolTipText = alert ? "LifeCharter · attacks still undefended" : "LifeCharter";
    }

    /// <summary>Progression depends on the date; recompute once the day changes.</summary>
    private void StartDayTimer(AppServices appServices)
    {
        var lastDay = appServices.Session.Today;
        dayTimer = new DispatcherTimer(DayCheckInterval, DispatcherPriority.Background, async (_, _) =>
        {
            try
            {
                RefreshTray();
                if (appServices.Session.Today != lastDay)
                {
                    lastDay = appServices.Session.Today;
                    await appServices.Session.ReloadAsync();
                    if (sync is not null)
                        await sync.BackupAsync();
                }

                if (reporter is not null)
                    await reporter.CheckAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Periodic check failed");
            }
        });
        dayTimer.Start();
    }

    private TrayIcon CreateTray()
    {
        var openTree = new NativeMenuItem("Open tree");
        openTree.Click += (_, _) => ShowTree();
        var quick = new NativeMenuItem("Quick entry…");
        quick.Click += (_, _) => ShowQuickEntry();
        var peekWidget = new NativeMenuItem("Peek widget");
        peekWidget.Click += (_, _) => TogglePeek();
        var position = new NativeMenuItem("Widget position") { Menu = CreateCornerMenu() };
        var autostart = new NativeMenuItem("Start on login")
        {
            ToggleType = MenuItemToggleType.CheckBox,
            IsChecked = AutostartService.IsEnabled(),
            IsVisible = AutostartService.IsSupported,
        };
        autostart.Click += (_, _) => _ = ToggleAutostartAsync(autostart);
        var siegeItem = new NativeMenuItem("Siege settings…");
        siegeItem.Click += (_, _) => ShowSiegeSettings();
        var syncItem = new NativeMenuItem("Sync…");
        syncItem.Click += (_, _) => ShowSync();
        var helpItem = new NativeMenuItem("Help");
        helpItem.Click += (_, _) => ShowHelp();
        var quit = new NativeMenuItem("Quit");
        quit.Click += (_, _) => _ = QuitAsync();

        var icon = new TrayIcon
        {
            Icon = TrayIconFactory.Create(),
            ToolTipText = "LifeCharter",
            // Today's defenses is inserted at index 3 once the chart is loaded.
            Menu = [openTree, quick, peekWidget, siegeItem, syncItem, position, autostart, helpItem, new NativeMenuItemSeparator(), quit],
        };
        icon.Clicked += (_, _) => ShowTree();
        return icon;
    }

    private NativeMenu CreateCornerMenu()
    {
        var menu = new NativeMenu();
        (WidgetCorner Corner, string Label)[] corners =
        [
            (WidgetCorner.TopLeft, "Top left"),
            (WidgetCorner.TopRight, "Top right"),
            (WidgetCorner.BottomLeft, "Bottom left"),
            (WidgetCorner.BottomRight, "Bottom right"),
        ];

        foreach (var (corner, label) in corners)
        {
            var item = new NativeMenuItem(label)
            {
                ToggleType = MenuItemToggleType.Radio,
                IsChecked = settings.WidgetCorner == corner,
            };
            item.Click += (_, _) => _ = ChangeCornerAsync(corner);
            cornerItems.Add(item);
            menu.Items.Add(item);
        }

        return menu;
    }

    private async Task ChangeCornerAsync(WidgetCorner corner)
    {
        // Picking a corner snaps the widget there and forgets a dragged position.
        settings = settings with { WidgetCorner = corner, WidgetPosition = null };
        for (var i = 0; i < cornerItems.Count; i++)
            cornerItems[i].IsChecked = (WidgetCorner)i == corner;

        try
        {
            await SettingsStore.SaveAsync(settings);
            await PlaceWidgetAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not change the widget corner");
        }
    }

    private async Task ToggleAutostartAsync(NativeMenuItem item)
    {
        var enable = !AutostartService.IsEnabled();
        try
        {
            await AutostartService.SetEnabledAsync(enable);
            logger.LogInformation("Start on login {State}", enable ? "enabled" : "disabled");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not change start on login");
        }

        item.IsChecked = AutostartService.IsEnabled();
    }

    /// <summary>Debounced: several size changes in a row place the widget once.</summary>
    private void SchedulePlacement()
    {
        replaceTimer ??= new DispatcherTimer(PlacementDelay, DispatcherPriority.Background, async (_, _) =>
        {
            replaceTimer!.Stop();
            try
            {
                await PlaceWidgetAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Widget placement failed");
            }
        });

        replaceTimer.Stop();
        replaceTimer.Start();
    }

    private async Task PlaceWidgetAsync()
    {
        if (widget is null || trackingDrag)
            return;

        var saved = settings.WidgetPosition;
        var placedByKWin = saved is not null
            ? await placer.PlaceAtAsync(WidgetWindow.AppId, saved)
            : await placer.PlaceAsync(WidgetWindow.AppId, settings.WidgetCorner, settings.WidgetMargin);

        if (placedByKWin)
        {
            // KWin applies the move once the widget acknowledges it with a new frame.
            await widget.CommitFramesAsync();
            lastPlacedPosition = await placer.ReadPositionAsync(widget.Title ?? "", WidgetWindow.AppId);
            return;
        }

        // Windows and X11: the window positions itself. Ignore the resulting PositionChanged.
        placing = true;
        try
        {
            if (saved is not null)
                widget.MoveTo(saved);
            else
                widget.MoveToCorner(settings.WidgetCorner, settings.WidgetMargin);
        }
        finally
        {
            placing = false;
        }
    }

    /// <summary>
    /// KDE Wayland: the app is not told when a compositor-driven drag ends, so it polls KWin
    /// until the widget stops moving, then saves where it landed.
    /// </summary>
    private async Task TrackDragAsync()
    {
        if (widget is null || trackingDrag || !KWinRuleInstaller.IsKdePlasma)
            return;

        trackingDrag = true;
        try
        {
            WidgetPosition? last = null;
            var stablePolls = 0;
            for (var i = 0; i < DragPollLimit && stablePolls < 2; i++)
            {
                await Task.Delay(DragPollInterval);
                var current = await placer.ReadPositionAsync(widget.Title ?? "", WidgetWindow.AppId);
                if (current is null)
                    return;

                stablePolls = current == last ? stablePolls + 1 : 0;
                last = current;
            }

            if (last is not null)
                await SavePositionAsync(last);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Tracking the widget drag failed");
        }
        finally
        {
            trackingDrag = false;
        }
    }

    /// <summary>Windows and X11 report moves; save the position once the user stops moving.</summary>
    private void SchedulePositionSave()
    {
        if (widget is null || placing || KWinRuleInstaller.IsKdePlasma)
            return;

        positionSaveTimer ??= new DispatcherTimer(TimeSpan.FromMilliseconds(500), DispatcherPriority.Background, async (_, _) =>
        {
            positionSaveTimer!.Stop();
            try
            {
                await SavePositionAsync(widget.CurrentPosition);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Saving the widget position failed");
            }
        });

        positionSaveTimer.Stop();
        positionSaveTimer.Start();
    }

    /// <summary>On quit, catch moves made outside the title row (Alt+drag, KWin shortcuts).</summary>
    private async Task SaveKWinPositionAsync()
    {
        if (widget is null || !KWinRuleInstaller.IsKdePlasma)
            return;

        try
        {
            var current = await placer.ReadPositionAsync(widget.Title ?? "", WidgetWindow.AppId)
                .WaitAsync(TimeSpan.FromSeconds(2));
            // Unchanged since the last placement means the user did not move it.
            if (current is not null && current != lastPlacedPosition)
                await SavePositionAsync(current);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not save the widget position on quit");
        }
    }

    private async Task SavePositionAsync(WidgetPosition position)
    {
        if (position == settings.WidgetPosition)
            return;

        settings = settings with { WidgetPosition = position };
        lastPlacedPosition = position;
        await SettingsStore.SaveAsync(settings);
        logger.LogInformation("Widget position saved: {X},{Y}", position.X, position.Y);
    }

    private void ShowTree()
    {
        if (services is null)
            return;

        if (tree is null)
        {
            tree = new TreeWindow(new TreeWindowViewModel(services.Session, ShowHelp, ShowSiegeSettings));
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

    private void ShowSiegeSettings()
    {
        if (services is null)
            return;

        if (siegeSettings is null)
        {
            siegeSettings = new SiegeSettingsWindow(new SiegeSettingsViewModel(services.Session));
            siegeSettings.Closed += (_, _) => siegeSettings = null;
            siegeSettings.Show();
        }

        siegeSettings.Activate();
    }

    private void ShowQuickEntry()
    {
        if (services is null)
            return;

        if (quickEntry is null)
        {
            quickEntry = new QuickEntryWindow(new QuickEntryViewModel(services.Session, EntrySource.QuickEntry, ShowPeek));
            quickEntry.Closed += (_, _) =>
            {
                quickEntry = null;
                logger.LogDebug("Quick entry closed");
            };
            quickEntry.Activated += (_, _) => logger.LogDebug("Quick entry activated");
            quickEntry.Deactivated += (_, _) => logger.LogDebug("Quick entry deactivated");
            quickEntry.Show();
            logger.LogDebug("Quick entry shown");
        }

        quickEntry.Activate();
    }

    private void ShowSync()
    {
        if (sync is null)
            return;

        if (syncWindow is null)
        {
            syncWindow = new SyncWindow(new SyncViewModel(sync, settings.SyncFolder, UseSyncFolderAsync));
            syncWindow.Closed += (_, _) => syncWindow = null;
            syncWindow.Show();
        }

        syncWindow.Activate();
    }

    private async Task UseSyncFolderAsync(string? folder)
    {
        if (sync is null)
            return;

        settings = settings with { SyncFolder = folder };
        await SettingsStore.SaveAsync(settings);
        await sync.UseFolderAsync(folder);
        logger.LogInformation("Sync folder set to {Folder}", folder ?? "(off)");
    }

    /// <summary>Opens the peek overlay, or keeps it open.</summary>
    private void ShowPeek()
    {
        if (peek is null)
            TogglePeek();
        else
            peek.Activate();
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
