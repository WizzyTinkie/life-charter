using LifeCharter.App.SkillTree.Business;
using LifeCharter.Core.Common.Business;
using LifeCharter.Core.QuickEntry.Models;

namespace LifeCharter.App.QuickEntry.Business;

/// <summary>Preview and confirmation lines for quick entry, in the widget, the popup and the CLI.</summary>
public static class QuickEntryText
{
    public const string Hint = "water 2l · run 30 min · vocab good · gym: legs · /undo · /today";

    /// <summary>"Stamina › Cardio · 30 min · +10 XP · defends today's attack"</summary>
    public static string Preview(ChartSession session, QuickEntryDraft draft, QuickEntryOutcome outcome) => draft.Kind switch
    {
        QuickEntryKind.None => Hint,
        QuickEntryKind.Invalid => draft.Message,
        QuickEntryKind.Today => "Enter: show today's defenses and to-dos",
        QuickEntryKind.Undo => session.LastEntryToday is { } last
            ? $"Enter: undo {Title(session, last.NodeId)} · {ProgressText.Entry(last, session.Zone, session.Today)}"
            : "Nothing logged today to undo",
        _ => "Enter: " + Describe(session, draft, outcome, preview: true),
    };

    /// <summary>"Logged Cardio · 30 min · +10 XP · defended"</summary>
    public static string Done(ChartSession session, QuickEntryDraft draft, QuickEntryOutcome outcome) => draft.Kind switch
    {
        QuickEntryKind.Undo => "Undone",
        QuickEntryKind.Today => "",
        QuickEntryKind.Recall => "Recalled " + Describe(session, draft, outcome, preview: false),
        _ => "Logged " + Describe(session, draft, outcome, preview: false),
    };

    private static string Describe(ChartSession session, QuickEntryDraft draft, QuickEntryOutcome outcome, bool preview)
    {
        var node = session.Chart.Nodes.First(n => n.Id == draft.NodeId);
        var parts = new List<string>
        {
            preview ? $"{node.Attribute} › {node.Title}" : node.Title,
            draft.Kind == QuickEntryKind.Recall ? $"recall {draft.Rating}"
                : string.IsNullOrWhiteSpace(draft.Unit) ? $"{draft.Amount:0.##}×"
                : $"{draft.Amount:0.##} {draft.Unit}",
        };

        if (outcome.XpGained > 0)
            parts.Add($"+{outcome.XpGained} XP");
        if (outcome.Defends)
            parts.Add(preview ? "defends today's attack" : "defended");
        if (outcome.Masters)
            parts.Add(preview ? "masters it!" : "mastered!");
        if (outcome.LevelUp)
            parts.Add("level up!");
        if (draft.Note.Length > 0)
            parts.Add($"“{draft.Note}”");
        if (preview && draft.Alternatives.Count > 0)
            parts.Add("also matches " + string.Join(", ", draft.Alternatives.Select(id => Title(session, id))));

        return string.Join(" · ", parts);
    }

    private static string Title(ChartSession session, Guid nodeId) =>
        session.Chart.Nodes.FirstOrDefault(node => node.Id == nodeId)?.Title ?? "?";
}
