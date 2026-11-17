using LifeCharter.Core.Goals.Models;
using LifeCharter.Core.Progression.Business;
using LifeCharter.Core.Progression.Models;
using LifeCharter.Core.SkillTree.Models;

namespace LifeCharter.Core.Tests.Progression.Business;

public sealed class AllocationRulesTests
{
    private readonly ChartBuilder chart = new();

    [Fact]
    public void Root_cannot_be_allocated() =>
        Assert.Equal(AllocationBlock.Root, Check(chart.Root));

    [Fact]
    public void Node_without_a_goal_cannot_be_allocated() =>
        Assert.Equal(AllocationBlock.NoGoal, Check(chart.Node("Sleep")));

    [Fact]
    public void Unconnected_node_is_not_connected()
    {
        var island = chart.Unconnected("Island");
        chart.Goal(island);

        Assert.Equal(AllocationBlock.NotConnected, Check(island));
    }

    [Fact]
    public void Unmet_requirement_is_reported()
    {
        var basics = chart.Node("Basics");
        var advanced = chart.Unconnected("Advanced");
        chart.Requires(basics, advanced);
        chart.Goal(advanced);

        Assert.Equal(AllocationBlock.RequirementsUnmet, Check(advanced));
    }

    [Fact]
    public void Available_node_with_a_goal_and_points_can_be_allocated()
    {
        var sleep = chart.Node("Sleep");
        chart.Goal(sleep);

        Assert.Equal(AllocationBlock.None, Check(sleep));
    }

    [Fact]
    public void Allocated_node_reports_already_allocated()
    {
        var sleep = chart.Node("Sleep");
        chart.Goal(sleep, target: 5);
        chart.Allocate(sleep);

        Assert.Equal(AllocationBlock.AlreadyAllocated, Check(sleep));
    }

    [Fact]
    public void Without_points_nothing_more_can_be_allocated()
    {
        foreach (var attribute in Enum.GetValues<LifeAttribute>())
        {
            var node = chart.Node(attribute.ToString(), attribute);
            chart.Goal(node, target: 5);
            chart.Allocate(node);
        }

        var extra = chart.Node("Extra");
        chart.Goal(extra);

        Assert.Equal(AllocationBlock.NoSkillPoints, Check(extra));
    }

    [Fact]
    public void Work_in_progress_limit_applies_per_attribute()
    {
        for (var i = 0; i < ProgressionRules.WorkInProgressLimit; i++)
        {
            var node = chart.Node($"Health {i}");
            chart.Goal(node, target: 5);
            chart.Allocate(node);
        }

        // Earn level 2 for one more point, logged on an unallocated node.
        var grinder = chart.Node("Grinder", LifeAttribute.Stamina);
        for (var day = 0; day < 4; day++)
        for (var i = 0; i < 3; i++)
            chart.Log(grinder, dayOffset: -day);

        var fourthHealth = chart.Node("Health 4");
        chart.Goal(fourthHealth);
        var stamina = chart.Node("Stamina", LifeAttribute.Stamina);
        chart.Goal(stamina);

        Assert.Equal(AllocationBlock.WorkInProgressLimit, Check(fourthHealth));
        Assert.Equal(AllocationBlock.None, Check(stamina));
    }

    [Fact]
    public void Only_nodes_in_training_can_be_refunded()
    {
        var training = chart.Node("Training");
        chart.Goal(training, target: 5);
        chart.Allocate(training);
        var mastered = chart.Node("Mastered");
        chart.Goal(mastered, GoalMetric.Milestone);
        chart.Allocate(mastered);
        chart.Log(mastered);

        var result = chart.Compute();
        Assert.True(AllocationRules.CanRefund(training.Id, result));
        Assert.False(AllocationRules.CanRefund(mastered.Id, result));
    }

    private AllocationBlock Check(SkillNode node) =>
        AllocationRules.Check(node.Id, chart.Build(), chart.Compute());
}
