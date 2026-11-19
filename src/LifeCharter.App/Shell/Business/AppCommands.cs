using LifeCharter.App.Shell.Models;

namespace LifeCharter.App.Shell.Business;

/// <summary>Parses command-line arguments and the wire format used between launches ("log run 5 km").</summary>
public static class AppCommands
{
    public const string Usage = """
        Usage: lifecharter [command]
          (none)        start, or open the tree if LifeCharter is running
          tree          open the skill tree
          peek          show the widget on top of everything
          quick         open the quick-entry popup
          log <text>    log something, for example: lifecharter log water 2l
          status        print level, today's defenses and wards
          help          open the help window
          quit          quit LifeCharter
        """;

    public static AppRequest Parse(IReadOnlyList<string> args) =>
        args.Count == 0 ? new AppRequest(AppCommand.Start) : FromWire(string.Join(' ', args));

    public static AppRequest FromWire(string value)
    {
        var text = value.Trim();
        var space = text.IndexOf(' ');
        var word = (space < 0 ? text : text[..space]).TrimStart('-').ToLowerInvariant();
        var argument = space < 0 ? "" : text[(space + 1)..].Trim();
        var command = word switch
        {
            "tree" => AppCommand.ShowTree,
            "peek" => AppCommand.Peek,
            "help" => AppCommand.Help,
            "quit" => AppCommand.Quit,
            "quick" => AppCommand.Quick,
            "log" => AppCommand.Log,
            "status" => AppCommand.Status,
            _ => AppCommand.Start,
        };
        return new AppRequest(command, argument);
    }

    public static string ToWire(AppRequest request)
    {
        var word = request.Command switch
        {
            AppCommand.ShowTree => "tree",
            AppCommand.Peek => "peek",
            AppCommand.Help => "help",
            AppCommand.Quit => "quit",
            AppCommand.Quick => "quick",
            AppCommand.Log => "log",
            AppCommand.Status => "status",
            _ => "start",
        };
        return request.Argument.Length == 0 ? word : $"{word} {request.Argument}";
    }
}
