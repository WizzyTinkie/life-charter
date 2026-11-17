using LifeCharter.Core.Common.Models;
using LifeCharter.Core.Goals.Models;
using LifeCharter.Core.Progression.Business;
using LifeCharter.Core.SkillTree.Models;

namespace LifeCharter.Core.SkillTree.Business;

/// <summary>
/// The first tree a new user gets (PLAN.md section 3): a root and one ring of categories per
/// attribute, each with a sensible starter goal. Everything can be edited afterwards.
/// </summary>
public static class StarterTemplate
{
    private const double Radius = 380;
    private const double SectorSpreadDegrees = 100;

    private static readonly Category[] Categories =
    [
        new(LifeAttribute.Health, "Sleep", RetentionModel.Habit, GoalMetric.Sum, 7, "h", GoalWindow.Daily, 14),
        new(LifeAttribute.Health, "Hydration", RetentionModel.Habit, GoalMetric.Sum, 2, "l", GoalWindow.Daily, 14),
        new(LifeAttribute.Health, "Nutrition", RetentionModel.Habit, GoalMetric.Count, 1, "healthy meal", GoalWindow.Daily, 14),
        new(LifeAttribute.Health, "Mind", RetentionModel.Habit, GoalMetric.Sum, 10, "min", GoalWindow.Daily, 14),
        new(LifeAttribute.Health, "Screen time", RetentionModel.Habit, GoalMetric.Count, 1, "screen-free evening", GoalWindow.Daily, 14),
        new(LifeAttribute.Health, "Social", RetentionModel.Habit, GoalMetric.Count, 1, "meetup", GoalWindow.Weekly, 4),
        new(LifeAttribute.Stamina, "Cardio", RetentionModel.Capacity, GoalMetric.Sum, 60, "min", GoalWindow.Weekly, 4),
        new(LifeAttribute.Stamina, "Strength", RetentionModel.Capacity, GoalMetric.Count, 2, "session", GoalWindow.Weekly, 4),
        new(LifeAttribute.Stamina, "Mobility", RetentionModel.Habit, GoalMetric.Sum, 10, "min", GoalWindow.Daily, 14),
        new(LifeAttribute.Stamina, "Steps", RetentionModel.Habit, GoalMetric.Sum, 8000, "steps", GoalWindow.Daily, 14),
        new(LifeAttribute.Stamina, "Sport", RetentionModel.MotorSkill, GoalMetric.Count, 1, "session", GoalWindow.Weekly, 4),
        new(LifeAttribute.Might, "Languages", RetentionModel.Memory, GoalMetric.Sum, 15, "min", GoalWindow.Daily, 14),
        new(LifeAttribute.Might, "Programming", RetentionModel.Memory, GoalMetric.Sum, 3, "h", GoalWindow.Weekly, 4),
        new(LifeAttribute.Might, "Reading", RetentionModel.Memory, GoalMetric.Sum, 100, "pages", GoalWindow.Total, 1),
        new(LifeAttribute.Might, "Courses", RetentionModel.Memory, GoalMetric.Milestone, 1, "", GoalWindow.Total, 1),
        new(LifeAttribute.Might, "Creative", RetentionModel.MotorSkill, GoalMetric.Count, 2, "session", GoalWindow.Weekly, 4),
    ];

    /// <summary>Sector centre per attribute, in degrees (screen coordinates, y down).</summary>
    public static double SectorCenterDegrees(LifeAttribute attribute) => attribute switch
    {
        LifeAttribute.Might => -90,
        LifeAttribute.Health => 30,
        _ => 150,
    };

    public static LifeChartData Create(DateOnly startDate)
    {
        var root = new SkillNode
        {
            Attribute = LifeAttribute.Might,
            Kind = NodeKind.Root,
            Title = "You",
            RetentionModel = RetentionModel.Milestone,
        };

        var nodes = new List<SkillNode> { root };
        var edges = new List<SkillEdge>();
        var goals = new List<Goal>();

        foreach (var group in Categories.GroupBy(category => category.Attribute))
        {
            var items = group.ToList();
            for (var i = 0; i < items.Count; i++)
            {
                var category = items[i];
                var t = items.Count == 1 ? 0.5 : i / (double)(items.Count - 1);
                var angle = (SectorCenterDegrees(category.Attribute) + (t - 0.5) * SectorSpreadDegrees) * Math.PI / 180;

                var node = new SkillNode
                {
                    Attribute = category.Attribute,
                    Kind = NodeKind.Small,
                    Title = category.Title,
                    X = Math.Round(Math.Cos(angle) * Radius, 1),
                    Y = Math.Round(Math.Sin(angle) * Radius, 1),
                    RetentionModel = category.Model,
                    XpReward = ProgressionRules.DefaultXpReward(NodeKind.Small),
                };
                nodes.Add(node);
                edges.Add(new SkillEdge { FromNodeId = root.Id, ToNodeId = node.Id, Kind = EdgeKind.Path });
                goals.Add(new Goal
                {
                    NodeId = node.Id,
                    Title = GoalTitle(category),
                    Metric = category.Metric,
                    Target = category.Target,
                    Unit = category.Unit,
                    Window = category.Window,
                    Periods = category.Periods,
                    StartDate = startDate,
                    Source = GoalSource.Template,
                });
            }
        }

        return new LifeChartData(nodes, edges, goals, [], [], []);
    }

    private static string GoalTitle(Category category) => category.Metric switch
    {
        GoalMetric.Milestone => $"Finish one {category.Title.ToLowerInvariant().TrimEnd('s')}",
        _ => category.Window switch
        {
            GoalWindow.Daily => $"{category.Target:0.##} {category.Unit} a day, {category.Periods} days",
            GoalWindow.Weekly => $"{category.Target:0.##} {category.Unit} a week, {category.Periods} weeks",
            _ => $"{category.Target:0.##} {category.Unit} in total",
        },
    };

    private sealed record Category(
        LifeAttribute Attribute,
        string Title,
        RetentionModel Model,
        GoalMetric Metric,
        double Target,
        string Unit,
        GoalWindow Window,
        int Periods);
}
