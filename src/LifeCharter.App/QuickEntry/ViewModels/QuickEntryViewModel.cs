using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LifeCharter.App.QuickEntry.Business;
using LifeCharter.Core.Common.Business;
using LifeCharter.Core.Journal.Models;
using LifeCharter.Core.QuickEntry.Models;

namespace LifeCharter.App.QuickEntry.ViewModels;

/// <summary>One quick-entry line: parses as you type, previews what Enter would do, applies it.</summary>
public sealed partial class QuickEntryViewModel(ChartSession session, EntrySource source, Action showToday) : ObservableObject
{
    private QuickEntryDraft draft = QuickEntryDraft.Empty;

    [ObservableProperty]
    public partial string Text { get; set; } = "";

    [ObservableProperty]
    public partial string Preview { get; set; } = QuickEntryText.Hint;

    [ObservableProperty]
    public partial bool IsInvalid { get; set; }

    /// <summary>Raised after Enter did something.</summary>
    public event EventHandler? Submitted;

    partial void OnTextChanged(string value)
    {
        draft = session.ParseQuickEntry(value);
        Preview = QuickEntryText.Preview(session, draft, session.Preview(draft));
        IsInvalid = draft.Kind == QuickEntryKind.Invalid;
    }

    [RelayCommand]
    private async Task SubmitAsync()
    {
        if (!draft.CanApply)
            return;

        var applied = draft;
        var outcome = session.Preview(applied);
        if (applied.Kind == QuickEntryKind.Today)
            showToday();
        else if (!await session.ApplyAsync(applied, source))
            return;

        Text = "";
        Preview = QuickEntryText.Done(session, applied, outcome) is { Length: > 0 } done ? done : QuickEntryText.Hint;
        Submitted?.Invoke(this, EventArgs.Empty);
    }
}
