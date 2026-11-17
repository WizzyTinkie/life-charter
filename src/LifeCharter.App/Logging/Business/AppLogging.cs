using Microsoft.Extensions.Logging;
using Serilog.Events;
using Serilog.Extensions.Logging;
using Serilog;

namespace LifeCharter.App.Logging.Business;

/// <summary>
/// Serilog writes rolling daily log files. Libraries log through
/// <see cref="Microsoft.Extensions.Logging.ILogger{TCategoryName}"/>, bridged to Serilog here.
/// </summary>
public static class AppLogging
{
    private const string OutputTemplate =
        "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}";

    private static SerilogLoggerFactory? factory;

    public static string LogDirectory { get; } = ResolveLogDirectory();

    public static void Initialize()
    {
        var debug = Environment.GetEnvironmentVariable("LIFECHARTER_DEBUG") == "1";

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Is(debug ? LogEventLevel.Debug : LogEventLevel.Information)
            .Enrich.FromLogContext()
            .WriteTo.File(
                Path.Combine(LogDirectory, "lifecharter-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                fileSizeLimitBytes: 10 * 1024 * 1024,
                rollOnFileSizeLimit: true,
                outputTemplate: OutputTemplate)
            .CreateLogger();

        factory = new SerilogLoggerFactory(Log.Logger, dispose: false);
    }

    public static ILogger<T> CreateLogger<T>() =>
        (factory ?? throw new InvalidOperationException("Call AppLogging.Initialize first.")).CreateLogger<T>();

    private static string ResolveLogDirectory()
    {
        if (OperatingSystem.IsWindows())
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LifeCharter", "logs");

        var stateHome = Environment.GetEnvironmentVariable("XDG_STATE_HOME");
        if (string.IsNullOrEmpty(stateHome))
            stateHome = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "state");

        return Path.Combine(stateHome, "LifeCharter", "logs");
    }
}
