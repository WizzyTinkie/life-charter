using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LifeCharter.App.SkillTree.Business;
using LifeCharter.Core.Common.Business;
using LifeCharter.Core.Goals.Business;
using LifeCharter.Core.Goals.Models;
using LifeCharter.Core.Journal.Models;
using LifeCharter.Core.Progression.Models;

namespace LifeCharter.App.SkillTree.ViewModels;

/// <summary>One goal in the inspector: progress, a log row and (in edit mode) its settings.</summary>
public sealed partial class GoalItemViewModel : ObservableObject
{
    private readonly ChartSession session;
    private readonly Goal goal;

    public GoalItemViewModel(ChartSession session, Goal goal, GoalProgress progress, bool isEditing)
    {
        this.session = session;
        this.goal = goal;
        IsEditing = isEditing;
        Fraction = progress.Fraction;
        ProgressDescription = ProgressText.Goal(goal, progress);
        IsComplete = progress.IsComplete;
        Title = goal.Title;
        Metric = goal.Metric;
        Target = (decimal)goal.Target;
        Unit = goal.Unit;
        Window = goal.Window;
        Periods = goal.Periods;
        Consecutive = goal.Consecutive;
        LogAmount = (decimal)GoalDefaults.Amount(goal);
    }

    public static IReadOnlyList<GoalMetric> Metrics { get; } = Enum.GetValues<GoalMetric>();

    public static IReadOnlyList<GoalWindow> Windows { get; } = Enum.GetValues<GoalWindow>();

    public bool IsEditing { get; }

    public double Fraction { get; }

    public string ProgressDescription { get; }

    public bool IsComplete { get; }

    public string UnitLabel => string.IsNullOrWhiteSpace(goal.Unit) ? "×" : goal.Unit;

    [ObservableProperty]
    public partial string Title { get; set; }

    [ObservableProperty]
    public partial GoalMetric Metric { get; set; }

    [ObservableProperty]
    public partial decimal? Target { get; set; }

    [ObservableProperty]
    public partial string Unit { get; set; }

    [ObservableProperty]
    public partial GoalWindow Window { get; set; }

    [ObservableProperty]
    public partial decimal? Periods { get; set; }

    [ObservableProperty]
    public partial bool Consecutive { get; set; }

    [ObservableProperty]
    public partial decimal? LogAmount { get; set; }

    [ObservableProperty]
    public partial string LogNote { get; set; } = "";

    [RelayCommand]
    private Task LogAsync() =>
        session.LogAsync(goal.NodeId, goal.Id, (double)(LogAmount ?? 1), goal.Unit, LogNote.Trim(), EntrySource.Manual);

    [RelayCommand]
    private Task SaveAsync()
    {
        goal.Title = Title.Trim();
        goal.Metric = Metric;
        goal.Target = (double)(Target ?? 1);
        goal.Unit = Unit.Trim();
        goal.Window = Window;
        goal.Periods = (int)(Periods ?? 1);
        goal.Consecutive = Consecutive;
        return session.SaveGoalAsync(goal);
    }

    [RelayCommand]
    private Task DeleteAsync() => session.DeleteGoalAsync(goal.Id);
}
