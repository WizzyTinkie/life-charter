using LifeCharter.Core.Journal.Models;

namespace LifeCharter.Core.Journal.Business;

public static class JournalRules
{
    /// <summary>Entries that count: neither retractions nor entries that were retracted.</summary>
    public static IEnumerable<LogEntry> EffectiveEntries(IEnumerable<LogEntry> entries)
    {
        var all = entries as IReadOnlyCollection<LogEntry> ?? entries.ToList();
        var retracted = all
            .Where(entry => entry.RetractsEntryId is not null)
            .Select(entry => entry.RetractsEntryId!.Value)
            .ToHashSet();

        return all.Where(entry => entry.RetractsEntryId is null && !retracted.Contains(entry.Id));
    }
}
