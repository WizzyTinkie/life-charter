using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LifeCharter.Core.Common.Business;
using LifeCharter.Core.Retention.Models;

namespace LifeCharter.App.Siege.ViewModels;

/// <summary>The four SRBAI statements (Gardner 2012), each answered from 1 (disagree) to 5 (agree).</summary>
public sealed partial class SelfCheckViewModel(ChartSession session, Guid nodeId, string title) : ObservableObject
{
    public string Intro { get; } = $"How true is this about \"{title}\"? 1 = not at all, 5 = completely.";

    [ObservableProperty]
    public partial double Automatically { get; set; } = 3;

    [ObservableProperty]
    public partial double WithoutRemembering { get; set; } = 3;

    [ObservableProperty]
    public partial double WithoutThinking { get; set; } = 3;

    [ObservableProperty]
    public partial double BeforeRealizing { get; set; } = 3;

    [RelayCommand]
    private Task SubmitAsync() => session.AddHabitCheckAsync(new HabitCheck
    {
        NodeId = nodeId,
        Automatically = (int)Math.Round(Automatically),
        WithoutRemembering = (int)Math.Round(WithoutRemembering),
        WithoutThinking = (int)Math.Round(WithoutThinking),
        BeforeRealizing = (int)Math.Round(BeforeRealizing),
    });
}
