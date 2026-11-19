using LifeCharter.Core.Retention.Models;

namespace LifeCharter.Core.QuickEntry.Models;

/// <summary>
/// What a quick-entry line would do. Nothing is stored until the draft is applied, so the UI can
/// preview it while you type. <see cref="Alternatives"/> lists other nodes that matched almost
/// as well.
/// </summary>
public sealed record QuickEntryDraft(
    QuickEntryKind Kind,
    Guid? NodeId = null,
    Guid? GoalId = null,
    double Amount = 1,
    string Unit = "",
    ReviewRating? Rating = null,
    string Note = "",
    string Message = "")
{
    public static QuickEntryDraft Empty { get; } = new(QuickEntryKind.None);

    public IReadOnlyList<Guid> Alternatives { get; init; } = [];

    public bool CanApply => Kind switch
    {
        QuickEntryKind.Log or QuickEntryKind.Recall => NodeId is not null,
        QuickEntryKind.Undo or QuickEntryKind.Today => true,
        _ => false,
    };

    public static QuickEntryDraft Invalid(string message) => new(QuickEntryKind.Invalid, Message: message);
}
