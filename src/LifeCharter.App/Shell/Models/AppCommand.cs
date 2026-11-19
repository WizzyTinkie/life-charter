namespace LifeCharter.App.Shell.Models;

/// <summary>What a launch of <c>lifecharter</c> asks the (single) running instance to do.</summary>
public enum AppCommand
{
    Start,
    ShowTree,
    Peek,
    Help,
    Quit,

    /// <summary>Open the quick-entry popup.</summary>
    Quick,

    /// <summary>Apply a quick-entry line: <c>lifecharter log water 2l</c>.</summary>
    Log,

    /// <summary>Print level, today's defenses and wards.</summary>
    Status,
}
