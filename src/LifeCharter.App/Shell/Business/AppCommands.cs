using LifeCharter.App.Shell.Models;

namespace LifeCharter.App.Shell.Business;

/// <summary>Parses command-line arguments and the wire format used between launches.</summary>
public static class AppCommands
{
    public static AppCommand Parse(IReadOnlyList<string> args) =>
        args.Count == 0 ? AppCommand.Start : FromWire(args[0]);

    public static AppCommand FromWire(string value) => value.Trim().ToLowerInvariant() switch
    {
        "tree" or "--tree" => AppCommand.ShowTree,
        "peek" or "--peek" => AppCommand.Peek,
        "quit" or "--quit" => AppCommand.Quit,
        _ => AppCommand.Start,
    };

    public static string ToWire(AppCommand command) => command switch
    {
        AppCommand.ShowTree => "tree",
        AppCommand.Peek => "peek",
        AppCommand.Quit => "quit",
        _ => "start",
    };
}
