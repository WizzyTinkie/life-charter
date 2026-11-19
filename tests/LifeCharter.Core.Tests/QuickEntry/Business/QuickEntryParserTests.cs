using LifeCharter.Core.Common.Models;
using LifeCharter.Core.Goals.Models;
using LifeCharter.Core.Progression.Business;
using LifeCharter.Core.Progression.Models;
using LifeCharter.Core.QuickEntry.Business;
using LifeCharter.Core.QuickEntry.Models;
using LifeCharter.Core.Retention.Models;
using LifeCharter.Core.SkillTree.Business;
using LifeCharter.Core.SkillTree.Models;

namespace LifeCharter.Core.Tests.QuickEntry.Business;

public sealed class QuickEntryParserTests
{
    private static readonly DateOnly Today = new(2026, 11, 19);

    private readonly LifeChartData chart = StarterTemplate.Create(Today.AddDays(-7));
    private readonly ProgressionResult progression;

    public QuickEntryParserTests() => progression = ProgressionCalculator.Compute(chart, Today, TimeZoneInfo.Utc);

    [Theory]
    [InlineData("water 2l", "Hydration", 2, "l")]
    [InlineData("water 500 ml", "Hydration", 0.5, "l")]
    [InlineData("2,5 l water", "Hydration", 2.5, "l")]
    [InlineData("Hydration", "Hydration", 2, "l")]
    [InlineData("run 30 min", "Cardio", 30, "min")]
    [InlineData("run 1.5 h", "Cardio", 90, "min")]
    [InlineData("read 30 pages", "Reading", 30, "pages")]
    [InlineData("read 30 stran", "Reading", 30, "pages")]
    [InlineData("walk 9000", "Steps", 9000, "steps")]
    [InlineData("gym", "Strength", 1, "session")]
    [InlineData("code 2 hodiny", "Programming", 2, "h")]
    [InlineData("screen time", "Screen time", 1, "screen-free evening")]
    public void Log_lines_find_the_node_goal_amount_and_unit(string text, string title, double amount, string unit)
    {
        var draft = Parse(text);

        Assert.Equal(QuickEntryKind.Log, draft.Kind);
        Assert.Equal(title, Title(draft));
        Assert.Equal(amount, draft.Amount, 3);
        Assert.Equal(unit, draft.Unit);
        Assert.Equal(chart.Goals.Single(goal => goal.NodeId == draft.NodeId).Id, draft.GoalId);
    }

    [Fact]
    public void Matching_ignores_case_and_diacritics()
    {
        var czech = chart.Nodes.Single(node => node.Title == "Languages");
        czech.Aliases = "Čeština";

        Assert.Equal("Languages", Title(Parse("cestina 20 min")));
    }

    [Fact]
    public void Rating_on_a_memory_node_is_a_recall()
    {
        var draft = Parse("vocab good");

        Assert.Equal(QuickEntryKind.Recall, draft.Kind);
        Assert.Equal("Languages", Title(draft));
        Assert.Equal(ReviewRating.Good, draft.Rating);
    }

    [Fact]
    public void Text_after_a_colon_is_the_note()
    {
        var draft = Parse("run 5 km: easy pace with Petr");

        Assert.Equal("Cardio", Title(draft));
        Assert.Equal("easy pace with Petr", draft.Note);
        Assert.Equal("km", draft.Unit);
        Assert.Null(draft.GoalId);
    }

    [Fact]
    public void A_unit_alone_finds_the_node_with_that_unit()
    {
        var draft = Parse("8000 steps");

        Assert.Equal("Steps", Title(draft));
    }

    [Fact]
    public void Unknown_words_are_invalid_with_a_hint()
    {
        var draft = Parse("juggling 10");

        Assert.Equal(QuickEntryKind.Invalid, draft.Kind);
        Assert.Contains("alias", draft.Message);
    }

    [Theory]
    [InlineData("/undo", QuickEntryKind.Undo)]
    [InlineData("/today", QuickEntryKind.Today)]
    [InlineData("/log water 1l", QuickEntryKind.Log)]
    [InlineData("/defend gym", QuickEntryKind.Log)]
    [InlineData("/defend vocab easy", QuickEntryKind.Recall)]
    [InlineData("/plan marathon", QuickEntryKind.Invalid)]
    [InlineData("/dance", QuickEntryKind.Invalid)]
    [InlineData("", QuickEntryKind.None)]
    public void Slash_commands(string text, QuickEntryKind kind) =>
        Assert.Equal(kind, Parse(text).Kind);

    [Fact]
    public void Defending_a_memory_node_needs_a_rating() =>
        Assert.Contains("good", Parse("/defend vocab").Message);

    [Fact]
    public void Allocated_nodes_win_a_tie()
    {
        var mind = chart.Nodes.Single(node => node.Title == "Mind");
        var mobility = chart.Nodes.Single(node => node.Title == "Mobility");
        mind.Aliases = "flow";
        mobility.Aliases = "flow";
        var allocated = chart with { Allocations = [new Allocation { NodeId = mobility.Id, Action = AllocationAction.Allocate, Timestamp = new DateTimeOffset(Today.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero) }] };

        var draft = QuickEntryParser.Parse("flow", allocated, ProgressionCalculator.Compute(allocated, Today, TimeZoneInfo.Utc));

        Assert.Equal("Mobility", Title(draft));
        Assert.Contains(mind.Id, draft.Alternatives);
    }

    [Fact]
    public void Preview_reports_xp_and_mastery()
    {
        var courses = chart.Nodes.Single(node => node.Title == "Courses");
        var allocated = chart with { Allocations = [new Allocation { NodeId = courses.Id, Action = AllocationAction.Allocate, Timestamp = new DateTimeOffset(Today.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero) }] };
        var before = ProgressionCalculator.Compute(allocated, Today, TimeZoneInfo.Utc);
        var draft = QuickEntryParser.Parse("course", allocated, before);

        var outcome = QuickEntryPreview.Evaluate(allocated, before, draft, new DateTimeOffset(Today.ToDateTime(new TimeOnly(18, 0)), TimeSpan.Zero), Today, TimeZoneInfo.Utc);

        Assert.True(outcome.Masters);
        Assert.Equal(ProgressionRules.EntryXp + courses.XpReward, outcome.XpGained);
    }

    private QuickEntryDraft Parse(string text) => QuickEntryParser.Parse(text, chart, progression);

    private string Title(QuickEntryDraft draft) => chart.Nodes.Single(node => node.Id == draft.NodeId).Title;
}
