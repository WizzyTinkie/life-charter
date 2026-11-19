using CommunityToolkit.Mvvm.ComponentModel;
using LifeCharter.Core.Siege.Models;

namespace LifeCharter.App.Siege.ViewModels;

/// <summary>One weekday toggle in the siege settings.</summary>
public sealed partial class RestDayViewModel(Weekdays day, Action changed) : ObservableObject
{
    public Weekdays Day { get; } = day;

    public string Label { get; } = day.ToString()[..3];

    [ObservableProperty]
    public partial bool IsRest { get; set; }

    partial void OnIsRestChanged(bool value) => changed();
}
