namespace LifeCharter.App.Help.Models;

public sealed record HelpSection(string Title, string Intro, IReadOnlyList<string> Points);
