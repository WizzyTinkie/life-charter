namespace LifeCharter.App.Shell.Models;

/// <summary>A command plus its argument, for example Log with "run 5 km".</summary>
public sealed record AppRequest(AppCommand Command, string Argument = "");
