using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LifeCharter.Core.Common.Business;
using LifeCharter.Core.Journal.Models;

namespace LifeCharter.App.Widget.ViewModels;

/// <summary>A goal of a node in training that still needs something today (or this week).</summary>
public sealed partial class TodayItemViewModel(ChartSession session, Guid nodeId, Guid goalId, string text, double amount, string unit)
    : ObservableObject
{
    public string Text { get; } = text;

    [ObservableProperty]
    public partial bool IsDone { get; set; }

    [RelayCommand]
    private async Task CompleteAsync()
    {
        IsDone = true;
        await session.LogAsync(nodeId, goalId, amount, unit, "", EntrySource.Widget);
    }
}
