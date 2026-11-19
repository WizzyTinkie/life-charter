using LifeCharter.Core.Common.Models;
using LifeCharter.Core.Journal.Models;
using LifeCharter.Core.Progression.Business;
using LifeCharter.Core.Retention.Business;
using LifeCharter.Core.Retention.Interfaces;
using LifeCharter.Core.Retention.Models;
using LifeCharter.Core.Siege.Models;
using LifeCharter.Core.SkillTree.Models;

namespace LifeCharter.Core.Siege.Business;

/// <summary>
/// Replays the siege day by day (PLAN.md section 5). A pure function of history, settings and
/// date: both devices compute identical outcomes, and nothing is resolved twice after a sync.
/// </summary>
/// <remarks>
/// For each day: attacks are chosen at the start of the day from the nodes that are due, most
/// dangerous first and within the daily budget; then the day's practice is fed to every node;
/// then undefended attacks deal damage, unless the day is shielded by a ward. Practice heals a
/// node to full integrity, or counts towards the cleanse of a corrupted one. Today is never
/// resolved: its attacks are shown with what has been defended so far.
/// </remarks>
public static class SiegeEngine
{
    public static SiegeResult Run(
        LifeChartData data,
        IReadOnlyDictionary<Guid, DateOnly> masteredOn,
        IReadOnlyList<LogEntry> effectiveEntries,
        DateOnly today,
        TimeZoneInfo zone)
    {
        var settings = data.SiegeSettings is { DeletedAt: null } synced ? synced : new SiegeSettings();
        var nodes = data.Nodes
            .Where(node => node.DeletedAt is null && masteredOn.ContainsKey(node.Id))
            .OrderBy(node => node.Id)
            .ToList();

        var practice = PracticeByNodeAndDay(effectiveEntries, data.HabitChecks, zone);
        var goalsByNode = data.Goals.Where(goal => goal.DeletedAt is null).ToLookup(goal => goal.NodeId);
        var trackers = nodes.ToDictionary(
            node => node.Id,
            node => RetentionTrackerFactory.Create(node, goalsByNode[node.Id].ToList(), masteredOn[node.Id], settings.DesiredRetention));
        var states = nodes.ToDictionary(node => node.Id, _ => new IntegrityState());
        var vacations = data.Vacations.Where(vacation => vacation.DeletedAt is null).ToList();
        var wardDays = data.WardUses.Select(use => use.Date).ToHashSet();

        var start = nodes
            .SelectMany(node => practice.TryGetValue(node.Id, out var days) ? days.Keys.Append(masteredOn[node.Id]) : [masteredOn[node.Id]])
            .Where(day => day <= today)
            .DefaultIfEmpty(today)
            .Min();

        var wards = settings.StartDate is { } siegeStart && siegeStart < start ? Math.Min(settings.WardCap, SiegeRules.StartingWards) : 0;
        var fullyDefendedDays = 0;
        var streak = 0;
        var bestStreak = 0;
        SiegeDay? todayReport = null;
        SiegeDay? yesterdayReport = null;

        for (var day = start; day <= today; day = day.AddDays(1))
        {
            if (day == settings.StartDate)
                wards = Math.Min(settings.WardCap, SiegeRules.StartingWards);

            var isToday = day == today;
            var paused = vacations.Any(vacation => vacation.StartDate <= day && day <= vacation.EndDate);
            var kind = settings.StartDate is not { } begin || day < begin ? SiegeDayKind.NotStarted
                : paused ? SiegeDayKind.Vacation
                : SiegeRules.IsRestDay(settings.RestDays, day) ? SiegeDayKind.Rest
                : SiegeDayKind.Siege;

            var attacks = kind == SiegeDayKind.Siege
                ? ChooseAttacks(nodes, masteredOn, trackers, states, settings, day)
                : [];

            var shielded = kind == SiegeDayKind.Siege && wardDays.Contains(day) && wards > 0;
            if (shielded)
                wards--;

            foreach (var node in nodes)
                trackers[node.Id].Advance(day, PracticeOn(practice, node.Id, day), paused);

            var results = new List<Attack>();
            foreach (var attack in attacks)
            {
                var tracker = trackers[attack.NodeId];
                var defended = tracker.Defends(PracticeOn(practice, attack.NodeId, day));
                results.Add(attack with { Defended = defended });
                if (isToday || defended || shielded || attack.Threat <= 0)
                    continue;

                tracker.Miss(day);
                Damage(states[attack.NodeId], attack.Threat, settings);
            }

            foreach (var node in nodes.Where(node => masteredOn[node.Id] <= day && trackers[node.Id].Defends(PracticeOn(practice, node.Id, day))))
                Heal(states[node.Id], settings);

            if (!isToday && kind == SiegeDayKind.Siege && results.Count > 0 && !shielded)
            {
                if (results.Any(attack => !attack.Defended && attack.Threat > 0))
                {
                    streak = 0;
                }
                else if (results.Any(attack => attack.Defended))
                {
                    streak++;
                    bestStreak = Math.Max(bestStreak, streak);
                    fullyDefendedDays++;
                    if (fullyDefendedDays % SiegeRules.DaysPerWard == 0)
                        wards = Math.Min(settings.WardCap, wards + 1);
                }
            }

            var report = new SiegeDay(day, kind, results, shielded);
            if (isToday)
                todayReport = report;
            else if (day == today.AddDays(-1))
                yesterdayReport = report;
        }

        var retention = nodes.ToDictionary(
            node => node.Id,
            node =>
            {
                var state = states[node.Id];
                return new NodeRetention(
                    node.Id,
                    masteredOn[node.Id],
                    trackers[node.Id].Status(today),
                    state.Integrity,
                    state.Corrupted,
                    state.Corrupted && settings.ZeroIntegrity == ZeroIntegrityEffect.Corrupt,
                    state.CleanseDone,
                    settings.CleanseSessions,
                    state.TimesCorrupted);
            });

        return new SiegeResult(
            settings.StartDate is not null,
            todayReport ?? new SiegeDay(today, SiegeDayKind.NotStarted, [], false),
            yesterdayReport,
            wards,
            settings.WardCap,
            SiegeRules.DaysPerWard - fullyDefendedDays % SiegeRules.DaysPerWard,
            streak,
            bestStreak,
            retention);
    }

    /// <summary>
    /// Due nodes, most dangerous first (highest threat, then lowest integrity), cut to the daily
    /// budget and interleaved across attributes so topics alternate.
    /// </summary>
    private static List<Attack> ChooseAttacks(
        IReadOnlyList<SkillNode> nodes,
        IReadOnlyDictionary<Guid, DateOnly> masteredOn,
        IReadOnlyDictionary<Guid, IRetentionTracker> trackers,
        IReadOnlyDictionary<Guid, IntegrityState> states,
        SiegeSettings settings,
        DateOnly day)
    {
        if (settings.AttacksPerDay <= 0)
            return [];

        var chosen = nodes
            .Where(node => masteredOn[node.Id] < day && !states[node.Id].Corrupted && trackers[node.Id].IsDue(day))
            .Select(node =>
            {
                var tracker = trackers[node.Id];
                var severity = tracker.Severity(day);
                var threat = SiegeRules.Damage(severity, tracker.IsFortified, settings.DamageMultiplier);
                return new Candidate(node.Attribute, new Attack(node.Id, severity, threat, false), states[node.Id].Integrity);
            })
            .OrderByDescending(candidate => candidate.Attack.Threat)
            .ThenByDescending(candidate => candidate.Attack.Severity)
            .ThenBy(candidate => candidate.Integrity)
            .Take(settings.AttacksPerDay)
            .ToList();

        var queues = chosen.GroupBy(candidate => candidate.Attribute).Select(group => new Queue<Candidate>(group)).ToList();
        var interleaved = new List<Attack>(chosen.Count);
        while (interleaved.Count < chosen.Count)
        {
            foreach (var queue in queues.Where(queue => queue.Count > 0))
                interleaved.Add(queue.Dequeue().Attack);
        }

        return interleaved;
    }

    private static void Damage(IntegrityState state, int damage, SiegeSettings settings)
    {
        state.Integrity = Math.Max(0, state.Integrity - damage);
        if (state.Integrity > 0 || settings.ZeroIntegrity == ZeroIntegrityEffect.Nothing)
            return;

        state.Corrupted = true;
        state.CleanseDone = 0;
        state.TimesCorrupted++;
    }

    private static void Heal(IntegrityState state, SiegeSettings settings)
    {
        if (state.Corrupted && ++state.CleanseDone < settings.CleanseSessions)
            return;

        state.Corrupted = false;
        state.CleanseDone = 0;
        state.Integrity = SiegeRules.MaxIntegrity;
    }

    private static DayPractice PracticeOn(Dictionary<Guid, Dictionary<DateOnly, DayPractice>> practice, Guid nodeId, DateOnly day) =>
        practice.TryGetValue(nodeId, out var days) && days.TryGetValue(day, out var done) ? done : DayPractice.None;

    private static Dictionary<Guid, Dictionary<DateOnly, DayPractice>> PracticeByNodeAndDay(
        IEnumerable<LogEntry> entries,
        IEnumerable<HabitCheck> checks,
        TimeZoneInfo zone)
    {
        var entriesByDay = entries.ToLookup(entry => new NodeDay(entry.NodeId, GoalProgressCalculator.LocalDate(entry.Timestamp, zone)));
        var checksByDay = checks.ToLookup(check => new NodeDay(check.NodeId, GoalProgressCalculator.LocalDate(check.Timestamp, zone)));

        return entriesByDay.Select(group => group.Key)
            .Union(checksByDay.Select(group => group.Key))
            .GroupBy(key => key.NodeId)
            .ToDictionary(
                group => group.Key,
                group => group.ToDictionary(
                    key => key.Day,
                    key =>
                    {
                        var dayEntries = entriesByDay[key].OrderBy(entry => entry.Timestamp).ToList();
                        return new DayPractice(
                            dayEntries.Count(entry => entry.Rating is null),
                            dayEntries.Where(entry => entry.Rating is not null).Select(entry => entry.Rating!.Value).ToList(),
                            checksByDay[key].OrderBy(check => check.Timestamp).ToList());
                    }));
    }

    private readonly record struct NodeDay(Guid NodeId, DateOnly Day);

    private readonly record struct Candidate(LifeAttribute Attribute, Attack Attack, int Integrity);

    private sealed class IntegrityState
    {
        public int Integrity { get; set; } = SiegeRules.MaxIntegrity;
        public bool Corrupted { get; set; }
        public int CleanseDone { get; set; }
        public int TimesCorrupted { get; set; }
    }
}
