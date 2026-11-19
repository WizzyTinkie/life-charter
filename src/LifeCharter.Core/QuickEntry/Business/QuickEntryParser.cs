using System.Globalization;
using System.Text.RegularExpressions;
using LifeCharter.Core.Common.Models;
using LifeCharter.Core.Goals.Business;
using LifeCharter.Core.Goals.Models;
using LifeCharter.Core.Progression.Models;
using LifeCharter.Core.QuickEntry.Models;
using LifeCharter.Core.Retention.Models;
using LifeCharter.Core.SkillTree.Business;
using LifeCharter.Core.SkillTree.Models;

namespace LifeCharter.Core.QuickEntry.Business;

/// <summary>
/// The deterministic quick-entry parser (PLAN.md section 8): "water 2l", "run 5 km", "read 30",
/// "czech good", "/undo". It finds a number with an optional unit, a recall rating, and matches
/// the remaining words against node titles, node aliases and quest aliases, ignoring case and
/// diacritics. Text after a colon becomes the note. No AI involved, so it is instant.
/// </summary>
public static partial class QuickEntryParser
{
    /// <summary>Other matches within this many points of the best one are offered as alternatives.</summary>
    private const int AmbiguityMargin = 10;

    private static readonly Dictionary<string, ReviewRating> RatingWords = new()
    {
        ["again"] = ReviewRating.Again, ["znovu"] = ReviewRating.Again,
        ["hard"] = ReviewRating.Hard, ["tezke"] = ReviewRating.Hard, ["tezko"] = ReviewRating.Hard,
        ["good"] = ReviewRating.Good, ["dobre"] = ReviewRating.Good, ["dobra"] = ReviewRating.Good,
        ["easy"] = ReviewRating.Easy, ["snadne"] = ReviewRating.Easy, ["lehke"] = ReviewRating.Easy, ["lehko"] = ReviewRating.Easy,
    };

    public static QuickEntryDraft Parse(string text, LifeChartData chart, ProgressionResult progression)
    {
        var input = text.Trim();
        var note = "";
        var colon = input.IndexOf(':');
        if (colon >= 0)
        {
            note = input[(colon + 1)..].Trim();
            input = input[..colon].Trim();
        }

        if (input.Length == 0)
            return QuickEntryDraft.Empty;

        return input.StartsWith('/')
            ? ParseCommand(input, note, chart, progression)
            : ParseLog(input, note, chart, progression, defend: false);
    }

    private static QuickEntryDraft ParseCommand(string input, string note, LifeChartData chart, ProgressionResult progression)
    {
        var space = input.IndexOf(' ');
        var command = TextNormalizer.Normalize(space < 0 ? input[1..] : input[1..space]);
        var rest = space < 0 ? "" : input[(space + 1)..].Trim();

        return command switch
        {
            "undo" => new QuickEntryDraft(QuickEntryKind.Undo),
            "today" => new QuickEntryDraft(QuickEntryKind.Today),
            "log" => ParseLog(rest, note, chart, progression, defend: false),
            "defend" => ParseLog(rest, note, chart, progression, defend: true),
            "goal" or "plan" or "coach" => QuickEntryDraft.Invalid("Goal planning with the AI coach arrives in Phase 5."),
            _ => QuickEntryDraft.Invalid($"Unknown command /{command}. Try /log, /defend, /undo or /today."),
        };
    }

    private static QuickEntryDraft ParseLog(string input, string note, LifeChartData chart, ProgressionResult progression, bool defend)
    {
        var goals = chart.Goals.Where(goal => goal.DeletedAt is null).ToList();
        var goalUnits = goals.Select(goal => TextNormalizer.Normalize(goal.Unit)).Where(unit => unit.Length > 0).ToHashSet();

        double? amount = null;
        var unit = "";
        ReviewRating? rating = null;
        var words = new List<string>();
        var tokens = input.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < tokens.Length; i++)
        {
            var token = TextNormalizer.Normalize(tokens[i]);
            var number = NumberPattern().Match(token);
            if (amount is null && number.Success)
            {
                amount = double.Parse(number.Groups[1].Value.Replace(',', '.'), CultureInfo.InvariantCulture);
                unit = number.Groups[2].Value;
                if (unit.Length == 0 && i + 1 < tokens.Length)
                {
                    var next = TextNormalizer.Normalize(tokens[i + 1]);
                    if (UnitAliases.IsKnown(next) || goalUnits.Contains(next))
                    {
                        unit = next;
                        i++;
                    }
                }

                continue;
            }

            if (rating is null && RatingWords.TryGetValue(token, out var word))
            {
                rating = word;
                continue;
            }

            words.Add(token);
        }

        var goalsByNode = goals.ToLookup(goal => goal.NodeId);
        var questAliases = chart.Quests
            .Where(quest => quest.DeletedAt is null)
            .ToLookup(quest => quest.NodeId, quest => quest.Aliases);
        var undefended = progression.Siege.Today.Attacks.Where(attack => !attack.Defended).Select(attack => attack.NodeId).ToHashSet();
        var query = string.Join(' ', words);

        var scored = chart.Nodes
            .Where(node => node.DeletedAt is null && node.Kind != NodeKind.Root)
            .Select(node =>
            {
                var hasUnitGoal = unit.Length > 0 && goalsByNode[node.Id].Any(goal => UnitAliases.Factor(unit, goal.Unit) is not null);
                var names = Names(node, questAliases[node.Id]);
                var nameScore = words.Count == 0 ? (hasUnitGoal ? 50 : 0) : names.Max(name => NameScore(name, query, words));
                var state = progression.Nodes.TryGetValue(node.Id, out var progress) ? progress.State : NodeState.Locked;
                var score = nameScore == 0 ? 0
                    : nameScore + (state.IsAllocated() ? 8 : 0) + (undefended.Contains(node.Id) ? 4 : 0) + (hasUnitGoal ? 6 : 0);
                return new { Node = node, Score = score };
            })
            .Where(match => match.Score > 0)
            .OrderByDescending(match => match.Score)
            .ThenBy(match => match.Node.Title, StringComparer.CurrentCulture)
            .ToList();

        if (scored.Count == 0)
        {
            return QuickEntryDraft.Invalid(words.Count == 0
                ? "Type what you did, for example \"water 2l\" or \"run 5 km\"."
                : $"No skill matches \"{query}\". Add it as an alias on the node (Edit tree).");
        }

        var best = scored[0];
        var alternatives = scored.Skip(1)
            .TakeWhile(match => match.Score >= best.Score - AmbiguityMargin)
            .Take(3)
            .Select(match => match.Node.Id)
            .ToList();
        var node = best.Node;

        if (rating is { } recall && node.RetentionModel is (RetentionModel.Memory or RetentionModel.MotorSkill))
            return new QuickEntryDraft(QuickEntryKind.Recall, node.Id, Amount: 1, Unit: "recall", Rating: recall, Note: note) { Alternatives = alternatives };

        if (defend && node.RetentionModel == RetentionModel.Memory)
            return QuickEntryDraft.Invalid($"A recall defends {node.Title}: recall first, then rate it, for example \"/defend {query} good\".");

        var choice = ChooseGoal(goalsByNode[node.Id].ToList(), amount, unit);
        return new QuickEntryDraft(QuickEntryKind.Log, node.Id, choice.Goal?.Id, choice.Amount, choice.Unit, Note: note) { Alternatives = alternatives };
    }

    /// <summary>
    /// The goal an entry counts for: the one whose unit matches (converting hours and minutes),
    /// otherwise a daily or weekly goal, otherwise the first. Without an amount, the goal's usual amount.
    /// </summary>
    private static GoalChoice ChooseGoal(List<Goal> goals, double? amount, string unit)
    {
        if (unit.Length > 0)
        {
            foreach (var goal in goals)
            {
                if (UnitAliases.Factor(unit, goal.Unit) is { } factor)
                    return new GoalChoice(goal, Math.Round((amount ?? 1) * factor, 2), goal.Unit);
            }

            return new GoalChoice(null, amount ?? 1, unit);
        }

        var chosen = goals.FirstOrDefault(goal => goal.Window != GoalWindow.Total) ?? goals.FirstOrDefault();
        return new GoalChoice(chosen, amount ?? (chosen is null ? 1 : GoalDefaults.Amount(chosen)), chosen?.Unit ?? "");
    }

    private static List<string> Names(SkillNode node, IEnumerable<string> questAliases) =>
        new[] { node.Title, node.Aliases }
            .Concat(questAliases)
            .SelectMany(text => text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Select(TextNormalizer.Normalize)
            .Where(name => name.Length > 0)
            .Distinct()
            .ToList();

    /// <summary>100 exact, 90 prefix, 75 every word a word prefix, 70 one word is the whole name, 60 substring.</summary>
    private static int NameScore(string name, string query, IReadOnlyList<string> queryWords)
    {
        if (name == query)
            return 100;

        if (name.StartsWith(query, StringComparison.Ordinal))
            return 90;

        var nameWords = TextNormalizer.Words(name).ToList();
        if (queryWords.All(word => nameWords.Any(nameWord => nameWord.StartsWith(word, StringComparison.Ordinal))))
            return 75;

        if (queryWords.Contains(name))
            return 70;

        return query.Length >= 3 && name.Contains(query, StringComparison.Ordinal) ? 60 : 0;
    }

    [GeneratedRegex(@"^(\d+(?:[.,]\d+)?)(\p{L}*)$")]
    private static partial Regex NumberPattern();

    private readonly record struct GoalChoice(Goal? Goal, double Amount, string Unit);
}
