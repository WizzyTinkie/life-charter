using LifeCharter.Core.Common.Models;
using LifeCharter.Core.Journal.Models;
using LifeCharter.Core.Progression.Business;
using LifeCharter.Core.Progression.Models;
using LifeCharter.Core.QuickEntry.Models;
using LifeCharter.Core.SkillTree.Business;

namespace LifeCharter.Core.QuickEntry.Business;

public static class QuickEntryPreview
{
    /// <summary>The journal entry a Log or Recall draft adds.</summary>
    public static LogEntry ToEntry(QuickEntryDraft draft, DateTimeOffset now, EntrySource source) => new()
    {
        NodeId = draft.NodeId ?? throw new InvalidOperationException("The draft has no node."),
        GoalId = draft.GoalId,
        Amount = draft.Amount,
        Unit = draft.Unit,
        Note = draft.Note,
        Rating = draft.Kind == QuickEntryKind.Recall ? draft.Rating : null,
        Source = source,
        Timestamp = now,
    };

    /// <summary>Computes progression as if the draft were applied and reports what would change.</summary>
    public static QuickEntryOutcome Evaluate(
        LifeChartData chart,
        ProgressionResult before,
        QuickEntryDraft draft,
        DateTimeOffset now,
        DateOnly today,
        TimeZoneInfo zone)
    {
        if (draft.Kind is not (QuickEntryKind.Log or QuickEntryKind.Recall) || draft.NodeId is not { } nodeId)
            return QuickEntryOutcome.Nothing;

        var entry = ToEntry(draft, now, EntrySource.Manual);
        var after = ProgressionCalculator.Compute(chart with { Entries = [.. chart.Entries, entry] }, today, zone);

        var wasUndefended = before.Siege.Today.Attacks.Any(attack => attack.NodeId == nodeId && !attack.Defended);
        var nowDefended = after.Siege.Today.Attacks.Any(attack => attack.NodeId == nodeId && attack.Defended);
        var wasMastered = before.Nodes.TryGetValue(nodeId, out var old) && old.State.IsMastered();
        var isMastered = after.Nodes.TryGetValue(nodeId, out var updated) && updated.State.IsMastered();

        return new QuickEntryOutcome(
            after.Character.TotalXp - before.Character.TotalXp,
            wasUndefended && nowDefended,
            !wasMastered && isMastered,
            after.Character.Level.Level > before.Character.Level.Level);
    }
}
