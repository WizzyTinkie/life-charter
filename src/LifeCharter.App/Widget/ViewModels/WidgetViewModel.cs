using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LifeCharter.App.SkillTree.Business;
using LifeCharter.Core.Common.Business;
using LifeCharter.Core.Goals.Models;
using LifeCharter.Core.SkillTree.Models;

namespace LifeCharter.App.Widget.ViewModels;

/// <summary>Level, attribute balance and today's to-dos for the desktop widget and peek window.</summary>
public sealed partial class WidgetViewModel : ObservableObject, IDisposable
{
    private const int MaxTodayItems = 6;

    private readonly ChartSession session;
    private readonly Action openTree;

    public WidgetViewModel(ChartSession session, Action openTree)
    {
        this.session = session;
        this.openTree = openTree;
        session.Changed += OnSessionChanged;
        Refresh();
    }

    public ObservableCollection<TodayItemViewModel> Today { get; } = [];

    [ObservableProperty]
    public partial string LevelText { get; set; } = "";

    [ObservableProperty]
    public partial double HealthShare { get; set; }

    [ObservableProperty]
    public partial double StaminaShare { get; set; }

    [ObservableProperty]
    public partial double MightShare { get; set; }

    [ObservableProperty]
    public partial string TodayHeader { get; set; } = "";

    public void Dispose() => session.Changed -= OnSessionChanged;

    [RelayCommand]
    private void OpenTree() => openTree();

    private void OnSessionChanged(object? sender, EventArgs e) => Dispatcher.UIThread.Post(Refresh);

    private void Refresh()
    {
        var character = session.Progression.Character;
        LevelText = $"Lv {character.Level.Level}";

        // Attribute bars show balance: the strongest attribute is full.
        var max = Math.Max(1, character.XpByAttribute.Values.Max());
        HealthShare = character.XpByAttribute[LifeAttribute.Health] * 100.0 / max;
        StaminaShare = character.XpByAttribute[LifeAttribute.Stamina] * 100.0 / max;
        MightShare = character.XpByAttribute[LifeAttribute.Might] * 100.0 / max;

        Today.Clear();
        var nodes = session.Chart.Nodes.ToDictionary(node => node.Id);
        var goals = session.Chart.Goals.ToDictionary(goal => goal.Id);
        var open = session.Progression.Nodes.Values
            .Where(progress => progress.State == NodeState.Training)
            .SelectMany(progress => progress.Goals.Select(goal => new { progress.NodeId, Goal = goals[goal.GoalId], Progress = goal }))
            .Where(item => !item.Progress.IsComplete &&
                           (item.Goal.Window == GoalWindow.Total || item.Progress.CurrentValue < item.Goal.Target))
            .ToList();

        foreach (var item in open.Take(MaxTodayItems))
        {
            var text = $"{nodes[item.NodeId].Title} · {ProgressText.Goal(item.Goal, item.Progress)}";
            Today.Add(new TodayItemViewModel(session, item.NodeId, item.Goal.Id, text, ProgressText.DefaultAmount(item.Goal), item.Goal.Unit));
        }

        TodayHeader = open.Count == 0
            ? session.Progression.Nodes.Values.Any(p => p.State == NodeState.Training) ? "All done for today 🎉" : "Allocate a node to start training"
            : $"Today · {open.Count} to do";
    }
}
