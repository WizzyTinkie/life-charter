namespace LifeCharter.App.Shell.Models;

/// <summary>What a launch of <c>lifecharter</c> asks the (single) running instance to do.</summary>
public enum AppCommand
{
    Start,
    ShowTree,
    Peek,
    Help,
    Quit,
}
