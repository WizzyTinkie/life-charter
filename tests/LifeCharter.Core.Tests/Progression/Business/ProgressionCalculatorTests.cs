using LifeCharter.Core.Goals.Models;
using LifeCharter.Core.Progression.Business;
using LifeCharter.Core.SkillTree.Models;

namespace LifeCharter.Core.Tests.Progression.Business;

public sealed class ProgressionCalculatorTests
{
    private readonly ChartBuilder chart = new();

    [Fact]
    public void Root_is_mastered_and_its_neighbours_are_available()
    {
        var sleep = chart.Node("Sleep");

        Assert.Equal(NodeState.Mastered, chart.StateOf(chart.Root));
        Assert.Equal(NodeState.Available, chart.StateOf(sleep));
    }

    [Fact]
    public void Node_further_out_unlocks_only_when_its_neighbour_is_mastered()
    {
        var cardio = chart.Node("Cardio");
        var halfMarathon = chart.Node("Half marathon", connectedTo: cardio);
        chart.Goal(cardio, GoalMetric.Milestone);
        Assert.Equal(NodeState.Locked, chart.StateOf(halfMarathon));

        chart.Allocate(cardio);
        Assert.Equal(NodeState.Training, chart.StateOf(cardio));
        Assert.Equal(NodeState.Locked, chart.StateOf(halfMarathon));

        chart.Log(cardio);
        Assert.Equal(NodeState.Mastered, chart.StateOf(cardio));
        Assert.Equal(NodeState.Available, chart.StateOf(halfMarathon));
    }

    [Fact]
    public void Mastering_grants_entry_xp_and_bonus_xp_to_the_nodes_attribute()
    {
        var languages = chart.Node("Languages", LifeAttribute.Might, xpReward: 50);
        chart.Goal(languages, GoalMetric.Milestone);
        chart.Allocate(languages);
        chart.Log(languages);

        var character = chart.Compute().Character;

        Assert.Equal(60, character.TotalXp);
        Assert.Equal(60, character.XpByAttribute[LifeAttribute.Might]);
        Assert.Equal(0, character.XpByAttribute[LifeAttribute.Health]);
    }

    [Fact]
    public void Only_the_first_entries_per_goal_and_day_earn_xp()
    {
        var water = chart.Node("Hydration");
        for (var i = 0; i < 5; i++)
            chart.Log(water, dayOffset: 0);
        for (var i = 0; i < 2; i++)
            chart.Log(water, dayOffset: -1);

        Assert.Equal((ProgressionRules.DailyEntryCapPerGoal + 2) * ProgressionRules.EntryXp, chart.Compute().Character.TotalXp);
    }

    [Fact]
    public void Skill_points_start_at_three_and_grow_with_levels()
    {
        var a = chart.Node("A");
        var b = chart.Node("B");
        chart.Goal(a, target: 1000);
        chart.Goal(b, target: 1000);
        chart.Allocate(a);
        chart.Allocate(b);
        Assert.Equal(1, chart.Compute().Character.SkillPoints);

        // 4 days × 3 capped entries × 10 XP = 120 XP → level 2 → one more point.
        for (var day = 0; day < 4; day++)
        for (var i = 0; i < 3; i++)
            chart.Log(a, dayOffset: -day);

        var character = chart.Compute().Character;
        Assert.Equal(2, character.Level.Level);
        Assert.Equal(2, character.SkillPoints);
    }

    [Fact]
    public void Mastering_a_node_returns_its_point()
    {
        var courses = chart.Node("Courses");
        chart.Goal(courses, GoalMetric.Milestone);
        chart.Allocate(courses);
        Assert.Equal(ProgressionRules.StartingSkillPoints - 1, chart.Compute().Character.SkillPoints);

        chart.Log(courses);

        var result = chart.Compute();
        Assert.Equal(NodeState.Mastered, result.Nodes[courses.Id].State);
        Assert.Equal(ProgressionRules.StartingSkillPoints, result.Character.SkillPoints);
    }

    [Fact]
    public void Refund_frees_the_node_and_its_point()
    {
        var mind = chart.Node("Mind");
        chart.Goal(mind, target: 10);
        chart.Allocate(mind);
        chart.Refund(mind);

        var result = chart.Compute();
        Assert.Equal(NodeState.Available, result.Nodes[mind.Id].State);
        Assert.Equal(ProgressionRules.StartingSkillPoints, result.Character.SkillPoints);
    }

    [Fact]
    public void Requires_edge_locks_until_the_prerequisite_is_mastered()
    {
        var basics = chart.Node("German A2");
        var advanced = chart.Unconnected("German B2");
        chart.Requires(basics, advanced);
        chart.Goal(basics, GoalMetric.Milestone);
        Assert.Equal(NodeState.Locked, chart.StateOf(advanced));

        chart.Allocate(basics);
        chart.Log(basics);
        Assert.Equal(NodeState.Available, chart.StateOf(advanced));
    }

    [Fact]
    public void Retracted_entry_no_longer_counts_for_goals_or_xp()
    {
        var courses = chart.Node("Courses", xpReward: 50);
        chart.Goal(courses, GoalMetric.Milestone);
        chart.Allocate(courses);
        var entry = chart.Log(courses);
        Assert.Equal(NodeState.Mastered, chart.StateOf(courses));

        chart.Retract(entry);

        var result = chart.Compute();
        Assert.Equal(NodeState.Training, result.Nodes[courses.Id].State);
        Assert.Equal(0, result.Character.TotalXp);
    }

    [Fact]
    public void Retraction_frees_the_daily_xp_cap_for_the_next_entry()
    {
        var water = chart.Node("Hydration");
        var entries = Enumerable.Range(0, ProgressionRules.DailyEntryCapPerGoal).Select(_ => chart.Log(water)).ToList();
        chart.Retract(entries[0]);
        chart.Log(water);

        Assert.Equal(ProgressionRules.DailyEntryCapPerGoal * ProgressionRules.EntryXp, chart.Compute().Character.TotalXp);
    }

    [Fact]
    public void Deleted_nodes_are_ignored()
    {
        var social = chart.Node("Social");
        chart.Log(social);
        chart.Delete(social);

        var result = chart.Compute();
        Assert.DoesNotContain(social.Id, result.Nodes.Keys);
        Assert.Equal(0, result.Character.TotalXp);
    }
}
