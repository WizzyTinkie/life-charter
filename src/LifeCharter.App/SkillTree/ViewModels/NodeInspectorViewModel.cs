using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LifeCharter.App.SkillTree.Business;
using LifeCharter.Core.Common.Business;
using LifeCharter.Core.Goals.Models;
using LifeCharter.Core.Progression.Models;
using LifeCharter.Core.SkillTree.Models;

namespace LifeCharter.App.SkillTree.ViewModels;

/// <summary>The side panel for the selected node. Rebuilt whenever the chart changes.</summary>
public sealed partial class NodeInspectorViewModel : ObservableObject
{
    private readonly ChartSession session;
    private readonly SkillNode node;

    public NodeInspectorViewModel(ChartSession session, SkillNode node, bool isEditing)
    {
        this.session = session;
        this.node = node;
        IsEditing = isEditing;

        var progress = session.Progression.Nodes[node.Id];
        State = progress.State;
        var block = session.CanAllocate(node.Id);
        CanAllocate = block == AllocationBlock.None;
        AllocationHint = progress.State == NodeState.Locked || (progress.State == NodeState.Available && !CanAllocate)
            ? ProgressText.Block(block)
            : "";
        CanRefund = session.CanRefund(node.Id);
        IsRoot = node.Kind == NodeKind.Root;

        var goalsById = session.Chart.Goals.Where(goal => goal.NodeId == node.Id).ToDictionary(goal => goal.Id);
        Goals = new ObservableCollection<GoalItemViewModel>(progress.Goals.Select(goalProgress =>
            new GoalItemViewModel(session, goalsById[goalProgress.GoalId], goalProgress, isEditing)));

        RecentEntries = new ObservableCollection<RecentEntryViewModel>(session.RecentEntries(node.Id, RecentEntryCount)
            .Select(entry => new RecentEntryViewModel(session, entry.Id, ProgressText.Entry(entry, session.Zone, session.Today))));

        Title = node.Title;
        Description = node.Description;
        Attribute = node.Attribute;
        Kind = node.Kind;
        RetentionModel = node.RetentionModel;
    }

    private const int RecentEntryCount = 5;

    public static IReadOnlyList<LifeAttribute> Attributes { get; } = Enum.GetValues<LifeAttribute>();

    public static IReadOnlyList<NodeKind> Kinds { get; } = [NodeKind.Small, NodeKind.Notable, NodeKind.Keystone];

    public static IReadOnlyList<RetentionModel> RetentionModels { get; } = Enum.GetValues<RetentionModel>();

    public bool IsEditing { get; }

    public bool IsRoot { get; }

    public bool CanEditNode => IsEditing && !IsRoot;

    public NodeState State { get; }

    public string Subtitle => IsRoot ? "Root" : $"{node.Attribute} · {node.Kind} · {State}";

    public bool CanAllocate { get; }

    public bool ShowAllocate => !IsRoot && State is NodeState.Available or NodeState.Locked;

    public string AllocationHint { get; }

    public bool CanRefund { get; }

    public bool HasGoals => Goals.Count > 0;

    public ObservableCollection<GoalItemViewModel> Goals { get; }

    public ObservableCollection<RecentEntryViewModel> RecentEntries { get; }

    public bool HasRecentEntries => RecentEntries.Count > 0;

    [ObservableProperty]
    public partial string Title { get; set; }

    [ObservableProperty]
    public partial string Description { get; set; }

    [ObservableProperty]
    public partial LifeAttribute Attribute { get; set; }

    [ObservableProperty]
    public partial NodeKind Kind { get; set; }

    [ObservableProperty]
    public partial RetentionModel RetentionModel { get; set; }

    [RelayCommand]
    private Task AllocateAsync() => session.AllocateAsync(node.Id);

    [RelayCommand]
    private Task RefundAsync() => session.RefundAsync(node.Id);

    [RelayCommand]
    private Task SaveNodeAsync()
    {
        node.Title = Title.Trim();
        node.Description = Description.Trim();
        node.Attribute = Attribute;
        if (node.Kind != Kind)
        {
            node.Kind = Kind;
            node.XpReward = Core.Progression.Business.ProgressionRules.DefaultXpReward(Kind);
        }

        node.RetentionModel = RetentionModel;
        return session.SaveNodeAsync(node);
    }

    [RelayCommand]
    private Task DeleteNodeAsync() => session.DeleteNodeAsync(node.Id);

    [RelayCommand]
    private Task AddGoalAsync() => session.SaveGoalAsync(new Goal
    {
        NodeId = node.Id,
        Title = "New goal",
        Metric = GoalMetric.Count,
        Target = 1,
        Window = GoalWindow.Daily,
        Periods = 14,
        StartDate = session.Today,
    });
}
