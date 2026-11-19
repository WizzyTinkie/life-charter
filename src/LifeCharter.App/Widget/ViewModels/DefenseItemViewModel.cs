using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LifeCharter.App.Siege.Business;
using LifeCharter.App.SkillTree.Business;
using LifeCharter.Core.Common.Business;
using LifeCharter.Core.Goals.Models;
using LifeCharter.Core.Journal.Models;
using LifeCharter.Core.Retention.Models;
using LifeCharter.Core.Siege.Models;
using LifeCharter.Core.SkillTree.Models;

namespace LifeCharter.App.Widget.ViewModels;

/// <summary>
/// One of today's attacks. A Memory node is defended with a rated recall; any other node by
/// ticking it, which logs the usual amount for its goal.
/// </summary>
public sealed partial class DefenseItemViewModel(ChartSession session, SkillNode node, Attack attack, Goal? goal) : ObservableObject
{
    public string Text { get; } = $"{node.Title} · {RetentionText.Attack(attack)}";

    public bool IsRecall { get; } = node.RetentionModel == RetentionModel.Memory;

    public bool IsTick => !IsRecall;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanRecall))]
    public partial bool IsDefended { get; set; } = attack.Defended;

    public bool CanRecall => IsRecall && !IsDefended;

    [RelayCommand]
    private async Task DefendAsync()
    {
        IsDefended = true;
        var amount = goal is null ? 1 : ProgressText.DefaultAmount(goal);
        await session.LogAsync(node.Id, goal?.Id, amount, goal?.Unit ?? "", "", EntrySource.Widget);
    }

    [RelayCommand]
    private async Task RecallAsync(ReviewRating rating)
    {
        IsDefended = true;
        await session.ReviewAsync(node.Id, rating, EntrySource.Widget);
    }
}
