using CommunityToolkit.Mvvm.Input;
using LifeCharter.Core.Common.Business;

namespace LifeCharter.App.SkillTree.ViewModels;

/// <summary>A recent log entry with an Undo button.</summary>
public sealed partial class RecentEntryViewModel(ChartSession session, Guid entryId, string text)
{
    public string Text { get; } = text;

    [RelayCommand]
    private Task UndoAsync() => session.UndoEntryAsync(entryId);
}
