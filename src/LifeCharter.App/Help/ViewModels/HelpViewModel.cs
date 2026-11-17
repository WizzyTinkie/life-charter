using LifeCharter.App.Help.Business;
using LifeCharter.App.Help.Models;

namespace LifeCharter.App.Help.ViewModels;

public sealed class HelpViewModel
{
    public IReadOnlyList<HelpSection> Sections => HelpContent.Sections;

    public IReadOnlyList<LevelRow> Levels => HelpContent.Levels;
}
