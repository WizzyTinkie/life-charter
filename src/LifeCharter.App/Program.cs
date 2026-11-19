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
        var request = AppCommands.Parse(args);
        if (args.Length > 0 && args[0] is "-h" or "--usage" or "/?")
        {
            Console.WriteLine(AppCommands.Usage);
            return 0;
        }

        try
        {
            // A plain second launch opens the tree in the running instance.
            var forwarded = request.Command == AppCommand.Start ? request with { Command = AppCommand.ShowTree } : request;
            if (SingleInstance.TryForward(AppCommands.ToWire(forwarded), out var reply))
            {
                Log.Information("Forwarded {Command} to the running instance", forwarded.Command);
                if (reply.Length > 0)
                    Console.WriteLine(reply);
                return 0;
            }

            Log.Information("LifeCharter starting with {Command}; logs in {LogDirectory}", request.Command, AppLogging.LogDirectory);
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
