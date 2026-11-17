using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LifeCharter.Core.Common.Business;
using LifeCharter.Core.SkillTree.Models;

namespace LifeCharter.App.SkillTree.ViewModels;

/// <summary>Header (level, XP, points) and the inspector of the main tree window.</summary>
public sealed partial class TreeWindowViewModel : ObservableObject, IDisposable
{
    private readonly ChartSession session;
    private readonly Action openHelp;

    public TreeWindowViewModel(ChartSession session, Action openHelp)
    {
        this.session = session;
        this.openHelp = openHelp;
        session.Changed += OnSessionChanged;
        Refresh();
    }

    public ChartSession Session => session;

    [ObservableProperty]
    public partial string LevelText { get; set; } = "";

    [ObservableProperty]
    public partial double XpFraction { get; set; }

    [ObservableProperty]
    public partial string XpText { get; set; } = "";

    [ObservableProperty]
    public partial string SkillPointsText { get; set; } = "";

    [ObservableProperty]
    public partial string AttributesText { get; set; } = "";

    [ObservableProperty]
    public partial bool IsEditing { get; set; }

    [ObservableProperty]
    public partial Guid? SelectedNodeId { get; set; }

    [ObservableProperty]
    public partial NodeInspectorViewModel? Inspector { get; set; }

    public void Dispose() => session.Changed -= OnSessionChanged;

    [RelayCommand]
    private void OpenHelp() => openHelp();

    partial void OnIsEditingChanged(bool value) => RefreshInspector();

    partial void OnSelectedNodeIdChanged(Guid? value) => RefreshInspector();

    private void OnSessionChanged(object? sender, EventArgs e) => Dispatcher.UIThread.Post(Refresh);

    private void Refresh()
    {
        var character = session.Progression.Character;
        LevelText = $"Lv {character.Level.Level}";
        XpFraction = character.Level.XpForNextLevel == 0 ? 0 : character.Level.XpIntoLevel / (double)character.Level.XpForNextLevel;
        XpText = $"{character.Level.XpIntoLevel} / {character.Level.XpForNextLevel} XP";
        SkillPointsText = character.SkillPoints == 1 ? "1 skill point" : $"{character.SkillPoints} skill points";
        AttributesText = string.Join("   ", Enum.GetValues<LifeAttribute>().Select(a => $"{a} {character.XpByAttribute[a]}"));
        RefreshInspector();
    }

    private void RefreshInspector()
    {
        var node = SelectedNodeId is { } id ? session.Chart.Nodes.FirstOrDefault(n => n.Id == id) : null;
        Inspector = node is null ? null : new NodeInspectorViewModel(session, node, IsEditing);
    }
}
