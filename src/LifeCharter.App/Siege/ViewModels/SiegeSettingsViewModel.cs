using System.Globalization;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LifeCharter.Core.Common.Business;
using LifeCharter.Core.Siege.Business;
using LifeCharter.Core.Siege.Models;

namespace LifeCharter.App.Siege.ViewModels;

/// <summary>
/// Difficulty preset and its knobs, rest days and vacation. Picking a preset fills in the knobs;
/// changing a knob turns the preset into Custom unless the knobs match another preset.
/// </summary>
public sealed partial class SiegeSettingsViewModel : ObservableObject, IDisposable
{
    private static readonly Weekdays[] Week =
    [
        Weekdays.Monday, Weekdays.Tuesday, Weekdays.Wednesday, Weekdays.Thursday, Weekdays.Friday, Weekdays.Saturday, Weekdays.Sunday,
    ];

    private readonly ChartSession session;
    private bool loading;

    public SiegeSettingsViewModel(ChartSession session)
    {
        this.session = session;
        RestDays = Week.Select(day => new RestDayViewModel(day, OnKnobChanged)).ToList();
        Load(session.Chart.SiegeSettings ?? new SiegeSettings());
        VacationStart = DateTime.Today;
        session.Changed += OnSessionChanged;
        RefreshStatus();
    }

    public static IReadOnlyList<Difficulty> Difficulties { get; } = Enum.GetValues<Difficulty>();

    public static IReadOnlyList<ZeroIntegrityEffect> Effects { get; } = Enum.GetValues<ZeroIntegrityEffect>();

    public IReadOnlyList<RestDayViewModel> RestDays { get; }

    [ObservableProperty]
    public partial Difficulty Difficulty { get; set; }

    [ObservableProperty]
    public partial decimal? AttacksPerDay { get; set; }

    [ObservableProperty]
    public partial decimal? DamageMultiplier { get; set; }

    [ObservableProperty]
    public partial decimal? WardCap { get; set; }

    [ObservableProperty]
    public partial ZeroIntegrityEffect ZeroIntegrity { get; set; }

    [ObservableProperty]
    public partial decimal? CleanseSessions { get; set; }

    [ObservableProperty]
    public partial decimal? DesiredRetention { get; set; }

    [ObservableProperty]
    public partial string StatusText { get; set; } = "";

    [ObservableProperty]
    public partial string SavedText { get; set; } = "";

    [ObservableProperty]
    public partial DateTime? VacationStart { get; set; }

    [ObservableProperty]
    public partial decimal? VacationDays { get; set; } = 7;

    [ObservableProperty]
    public partial string VacationText { get; set; } = "";

    [ObservableProperty]
    public partial bool HasVacation { get; set; }

    [ObservableProperty]
    public partial string EndVacationText { get; set; } = "";

    public void Dispose() => session.Changed -= OnSessionChanged;

    partial void OnDifficultyChanged(Difficulty value)
    {
        if (loading || value == Difficulty.Custom)
            return;

        var preset = new SiegeSettings();
        DifficultyPresets.Apply(preset, value);
        loading = true;
        LoadKnobs(preset);
        loading = false;
    }

    partial void OnAttacksPerDayChanged(decimal? value) => OnKnobChanged();

    partial void OnDamageMultiplierChanged(decimal? value) => OnKnobChanged();

    partial void OnWardCapChanged(decimal? value) => OnKnobChanged();

    partial void OnZeroIntegrityChanged(ZeroIntegrityEffect value) => OnKnobChanged();

    partial void OnCleanseSessionsChanged(decimal? value) => OnKnobChanged();

    [RelayCommand]
    private async Task SaveAsync()
    {
        var settings = session.Chart.SiegeSettings ?? new SiegeSettings { StartDate = session.Today };
        ApplyKnobs(settings);
        settings.Difficulty = DifficultyPresets.Detect(settings);
        await session.SaveSiegeSettingsAsync(settings);
        SavedText = "Saved. Today's attacks are recomputed with the new settings.";
    }

    [RelayCommand]
    private Task StartVacationAsync()
    {
        var start = DateOnly.FromDateTime(VacationStart ?? DateTime.Today);
        var days = Math.Max(1, (int)(VacationDays ?? 1));
        return session.StartVacationAsync(start, start.AddDays(days - 1));
    }

    [RelayCommand]
    private Task EndVacationAsync() =>
        PlannedVacation() is { } vacation ? session.EndVacationAsync(vacation.Id) : Task.CompletedTask;

    private void OnKnobChanged()
    {
        if (loading)
            return;

        var probe = new SiegeSettings();
        ApplyKnobs(probe);
        loading = true;
        Difficulty = DifficultyPresets.Detect(probe);
        loading = false;
    }

    private void OnSessionChanged(object? sender, EventArgs e) => Dispatcher.UIThread.Post(RefreshStatus);

    private void Load(SiegeSettings settings)
    {
        loading = true;
        Difficulty = DifficultyPresets.Detect(settings);
        LoadKnobs(settings);
        DesiredRetention = (decimal)settings.DesiredRetention;
        loading = false;
    }

    private void LoadKnobs(SiegeSettings settings)
    {
        AttacksPerDay = settings.AttacksPerDay;
        DamageMultiplier = (decimal)settings.DamageMultiplier;
        WardCap = settings.WardCap;
        ZeroIntegrity = settings.ZeroIntegrity;
        CleanseSessions = settings.CleanseSessions;
        foreach (var day in RestDays)
            day.IsRest = settings.RestDays.HasFlag(day.Day);
    }

    private void ApplyKnobs(SiegeSettings settings)
    {
        settings.AttacksPerDay = (int)(AttacksPerDay ?? 0);
        settings.DamageMultiplier = (double)(DamageMultiplier ?? 0);
        settings.WardCap = (int)(WardCap ?? 0);
        settings.ZeroIntegrity = ZeroIntegrity;
        settings.CleanseSessions = (int)(CleanseSessions ?? 1);
        settings.DesiredRetention = (double)(DesiredRetention ?? 0.9m);
        settings.RestDays = RestDays.Where(day => day.IsRest).Aggregate(Weekdays.None, (all, day) => all | day.Day);
    }

    private Vacation? PlannedVacation() =>
        session.CurrentVacation ??
        session.Chart.Vacations
            .Where(vacation => vacation.DeletedAt is null && vacation.StartDate > session.Today)
            .OrderBy(vacation => vacation.StartDate)
            .FirstOrDefault();

    private void RefreshStatus()
    {
        var siege = session.Progression.Siege;
        var started = session.Chart.SiegeSettings?.StartDate;
        StatusText = started is { } date
            ? $"Siege since {Format(date)} · wards {siege.Wards}/{siege.WardCap} (next after {siege.DaysToNextWard} fully defended days) · streak {siege.Streak}, best {siege.BestStreak}"
            : "The siege starts when you save these settings.";

        var vacation = PlannedVacation();
        HasVacation = vacation is not null;
        var current = vacation is not null && vacation.StartDate <= session.Today;
        VacationText = vacation is null ? ""
            : current ? $"On vacation until {Format(vacation.EndDate)}. No attacks until then."
            : $"Vacation planned {Format(vacation.StartDate)} – {Format(vacation.EndDate)}.";
        EndVacationText = current ? "End vacation now" : "Cancel vacation";
    }

    private static string Format(DateOnly date) => date.ToString("d MMM yyyy", CultureInfo.CurrentCulture);
}
