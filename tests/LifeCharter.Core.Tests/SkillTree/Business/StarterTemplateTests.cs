using LifeCharter.Core.Progression.Business;
using LifeCharter.Core.SkillTree.Business;
using LifeCharter.Core.SkillTree.Models;

namespace LifeCharter.Core.Tests.SkillTree.Business;

public sealed class StarterTemplateTests
{
    private static readonly DateOnly Start = new(2026, 11, 17);

    [Fact]
    public void Template_has_a_root_and_sixteen_categories_with_one_goal_each()
    {
        var chart = StarterTemplate.Create(Start);
        var categories = chart.Nodes.Where(node => node.Kind != NodeKind.Root).ToList();

        Assert.Single(chart.Nodes, node => node.Kind == NodeKind.Root);
        Assert.Equal(16, categories.Count);
        Assert.All(categories, node => Assert.Single(chart.Goals, goal => goal.NodeId == node.Id));
        Assert.Equal(6, categories.Count(node => node.Attribute == LifeAttribute.Health));
        Assert.Equal(5, categories.Count(node => node.Attribute == LifeAttribute.Stamina));
        Assert.Equal(5, categories.Count(node => node.Attribute == LifeAttribute.Might));
    }

    [Fact]
    public void Every_category_is_available_from_the_start()
    {
        var chart = StarterTemplate.Create(Start);
        var result = ProgressionCalculator.Compute(chart, Start, TimeZoneInfo.Utc);

        Assert.All(
            chart.Nodes.Where(node => node.Kind != NodeKind.Root),
            node => Assert.Equal(NodeState.Available, result.Nodes[node.Id].State));
        Assert.Equal(ProgressionRules.StartingSkillPoints, result.Character.SkillPoints);
    }
}
