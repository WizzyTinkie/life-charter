using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LifeCharter.App.Siege.Business;
using LifeCharter.App.SkillTree.Business;
using LifeCharter.Core.Common.Business;
using LifeCharter.Core.Goals.Models;
using LifeCharter.Core.Siege.Models;
using LifeCharter.Core.SkillTree.Models;

namespace LifeCharter.App.Widget.ViewModels;

/// <summary>Level, attribute balance, today's defense and to-dos for the desktop widget and peek window.</summary>
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

    public ObservableCollection<DefenseItemViewModel> Defense { get; } = [];

    [ObservableProperty]
    public partial bool ShowSiege { get; set; }

    [ObservableProperty]
    public partial string SiegeHeader { get; set; } = "";

    [ObservableProperty]
    public partial string SiegeNote { get; set; } = "";

    [ObservableProperty]
    public partial bool HasSiegeNote { get; set; }

    [ObservableProperty]
    public partial bool CanUseWard { get; set; }

    [ObservableProperty]
    public partial string WardText { get; set; } = "";

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

    [ObservableProperty]
    public partial string LastEntryText { get; set; } = "";

    [ObservableProperty]
    public partial bool HasLastEntry { get; set; }

    private Guid? lastEntryId;

    public void Dispose() => session.Changed -= OnSessionChanged;

    [RelayCommand]
    private void OpenTree() => openTree();

    [RelayCommand]
    private Task UseWardAsync() => session.UseWardAsync();

    [RelayCommand]
    private Task UndoLastAsync() => lastEntryId is { } id ? session.UndoEntryAsync(id) : Task.CompletedTask;

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

        var nodes = session.Chart.Nodes.ToDictionary(node => node.Id);
        var goals = session.Chart.Goals.ToDictionary(goal => goal.Id);
        RefreshSiege(nodes);

        Today.Clear();
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

        // Offer undo for today's most recent entry.
        var last = session.RecentEntries(null, 1).FirstOrDefault();
        var lastIsToday = last is not null &&
                          DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(last.Timestamp, session.Zone).DateTime) == session.Today;
        lastEntryId = lastIsToday ? last!.Id : null;
        HasLastEntry = lastIsToday;
        LastEntryText = lastIsToday && nodes.TryGetValue(last!.NodeId, out var lastNode)
            ? $"Last: {lastNode.Title} · {ProgressText.Entry(last, session.Zone, session.Today)}"
            : "";

        TodayHeader = open.Count == 0
            ? session.Progression.Nodes.Values.Any(p => p.State == NodeState.Training) ? "All done for today 🎉" : "Allocate a node to start training"
            : $"Today · {open.Count} to do";
    }

    private void RefreshSiege(IReadOnlyDictionary<Guid, SkillNode> nodes)
    {
        var siege = session.Progression.Siege;
        ShowSiege = siege.IsStarted;
        Defense.Clear();
        foreach (var attack in siege.Today.Attacks)
        {
            var goal = session.Chart.Goals.FirstOrDefault(g => g.NodeId == attack.NodeId && g.DeletedAt is null);
            Defense.Add(new DefenseItemViewModel(session, nodes[attack.NodeId], attack, goal));
        }

        var today = siege.Today;
        SiegeHeader = today.Kind != SiegeDayKind.Siege ? RetentionText.DayKind(today.Kind)
            : today.Shielded ? "Shielded by a ward today"
            : today.Attacks.Count == 0 ? "No attacks today"
            : today.Defended == today.Attacks.Count ? $"All {today.Attacks.Count} attacks defended"
            : $"Defend today · {today.Defended}/{today.Attacks.Count}";

        var notes = new List<string>();
        if (siege.Yesterday is { Damage: > 0 } yesterday)
            notes.Add($"Yesterday: −{yesterday.Damage} HP");
        var corrupted = siege.Nodes.Values.Count(node => node.IsCorrupted);
        if (corrupted > 0)
            notes.Add(corrupted == 1 ? "1 node needs a cleanse" : $"{corrupted} nodes need a cleanse");
        if (siege.Streak > 1)
            notes.Add($"streak {siege.Streak}");
        SiegeNote = string.Join(" · ", notes);
        HasSiegeNote = notes.Count > 0;

        CanUseWard = session.CanUseWard && today.Attacks.Any(attack => !attack.Defended && attack.Threat > 0);
        WardText = $"Ward ({siege.Wards})";
    }
}
