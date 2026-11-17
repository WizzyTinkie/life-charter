using Avalonia.Logging;
using Serilog;
using SerilogLevel = Serilog.Events.LogEventLevel;

namespace LifeCharter.App.Logging.Business;

/// <summary>Routes Avalonia's internal warnings and errors into the Serilog log file.</summary>
public sealed class AvaloniaSerilogSink : ILogSink
{
    private readonly ILogger logger = Serilog.Log.ForContext("SourceContext", "Avalonia");

    public bool IsEnabled(LogEventLevel level, string area) => level >= LogEventLevel.Warning;

    public void Log(LogEventLevel level, string area, object? source, string messageTemplate) =>
        Log(level, area, source, messageTemplate, []);

    public void Log(LogEventLevel level, string area, object? source, string messageTemplate, params object?[] propertyValues)
    {
        if (!IsEnabled(level, area))
            return;

        logger.ForContext("Area", area).Write(ToSerilog(level), "[" + area + "] " + messageTemplate, propertyValues);
    }

    private static SerilogLevel ToSerilog(LogEventLevel level) => level switch
    {
        LogEventLevel.Verbose => SerilogLevel.Verbose,
        LogEventLevel.Debug => SerilogLevel.Debug,
        LogEventLevel.Information => SerilogLevel.Information,
        LogEventLevel.Warning => SerilogLevel.Warning,
        LogEventLevel.Error => SerilogLevel.Error,
        _ => SerilogLevel.Fatal,
    };
}
