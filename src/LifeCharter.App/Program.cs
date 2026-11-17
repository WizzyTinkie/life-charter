using Avalonia;
using Avalonia.Logging;
using LifeCharter.App.Logging.Business;
using LifeCharter.App.Shell.Business;
using LifeCharter.App.Shell.Models;
using LifeCharter.Platform.AppInstance.Business;
using Serilog;

namespace LifeCharter.App;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        AppLogging.Initialize();
        var command = AppCommands.Parse(args);

        try
        {
            // A plain second launch opens the tree in the running instance.
            var forwarded = command == AppCommand.Start ? AppCommand.ShowTree : command;
            if (SingleInstance.TryForward(AppCommands.ToWire(forwarded)))
            {
                Log.Information("Forwarded {Command} to the running instance", forwarded);
                return 0;
            }

            Log.Information("LifeCharter starting with {Command}; logs in {LogDirectory}", command, AppLogging.LogDirectory);
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            return 0;
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "LifeCharter crashed");
            return 1;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    // Also used by the IDE previewer.
    public static AppBuilder BuildAvaloniaApp()
    {
        Logger.Sink = new AvaloniaSerilogSink();
        var builder = AppBuilder.Configure<App>().UsePlatformDetect();

        // LIFECHARTER_BACKEND=x11 forces XWayland, for comparing against native Wayland.
        return Environment.GetEnvironmentVariable("LIFECHARTER_BACKEND") == "x11"
            ? builder
            : builder.UseWaylandWithFallback();
    }
}
