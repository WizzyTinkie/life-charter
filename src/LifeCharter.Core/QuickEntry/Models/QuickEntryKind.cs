namespace LifeCharter.Core.QuickEntry.Models;

public enum QuickEntryKind
{
    /// <summary>Nothing typed yet.</summary>
    None,

    /// <summary>Not understood; <see cref="QuickEntryDraft.Message"/> says why.</summary>
    Invalid,

    Log,

    /// <summary>A rated recall session on a Memory or Motor skill node.</summary>
    Recall,

    /// <summary>Undo today's most recent entry.</summary>
    Undo,

    /// <summary>Show today's defenses and to-dos.</summary>
    Today,
}
