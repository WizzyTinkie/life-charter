using LifeCharter.Core.Common.Models;
using LifeCharter.Core.Goals.Models;
using LifeCharter.Core.Journal.Models;
using LifeCharter.Core.Progression.Business;
using LifeCharter.Core.Progression.Models;
using LifeCharter.Core.SkillTree.Models;

namespace LifeCharter.Core.Tests.Progression.Business;

/// <summary>Builds small charts for progression tests. "Today" is Tuesday 2026-11-17, UTC.</summary>
internal sealed class ChartBuilder
{
    public static readonly DateOnly Today = new(2026, 11, 17);

    private readonly List<SkillNode> nodes = [];
    private readonly List<SkillEdge> edges = [];
    private readonly List<Goal> goals = [];
    private readonly List<LogEntry> entries = [];
    private readonly List<Allocation> allocations = [];
    private int sequence;

    public ChartBuilder()
    {
        Root = new SkillNode { Kind = NodeKind.Root, Title = "You" };
        nodes.Add(Root);
    }

    public SkillNode Root { get; }

    public SkillNode Node(string title, LifeAttribute attribute = LifeAttribute.Health, SkillNode? connectedTo = null, int xpReward = 50)
    {
        var node = Unconnected(title, attribute, xpReward);
        edges.Add(new SkillEdge { FromNodeId = (connectedTo ?? Root).Id, ToNodeId = node.Id, Kind = EdgeKind.Path });
        return node;
    }

    public SkillNode Unconnected(string title, LifeAttribute attribute = LifeAttribute.Health, int xpReward = 50)
    {
        var node = new SkillNode { Title = title, Attribute = attribute, XpReward = xpReward };
        nodes.Add(node);
        return node;
    }

    public void Requires(SkillNode prerequisite, SkillNode node) =>
        edges.Add(new SkillEdge { FromNodeId = prerequisite.Id, ToNodeId = node.Id, Kind = EdgeKind.Requires });

    public Goal Goal(
        SkillNode node,
        GoalMetric metric = GoalMetric.Sum,
        double target = 1,
        string unit = "",
        GoalWindow window = GoalWindow.Total,
        int periods = 1,
        bool consecutive = false,
        int startOffset = -30)
    {
        var goal = new Goal
        {
            NodeId = node.Id,
            Metric = metric,
            Target = target,
            Unit = unit,
            Window = window,
            Periods = periods,
            Consecutive = consecutive,
            StartDate = Today.AddDays(startOffset),
        };
        goals.Add(goal);
        return goal;
    }

    public void Allocate(SkillNode node) => AddAllocation(node, AllocationAction.Allocate);

    public void Refund(SkillNode node) => AddAllocation(node, AllocationAction.Refund);

    public LogEntry Log(SkillNode node, double amount = 1, string unit = "", int dayOffset = 0, Goal? goal = null)
    {
        var entry = new LogEntry
        {
            NodeId = node.Id,
            GoalId = goal?.Id,
            Amount = amount,
            Unit = unit,
            Timestamp = At(dayOffset).AddMinutes(sequence++),
        };
        entries.Add(entry);
        return entry;
    }

    public void Retract(LogEntry entry) =>
        entries.Add(new LogEntry
        {
            NodeId = entry.NodeId,
            GoalId = entry.GoalId,
            Amount = 0,
            RetractsEntryId = entry.Id,
            Timestamp = entry.Timestamp.AddMinutes(1),
        });

    public void Delete(SkillNode node) => node.DeletedAt = "deleted";

    public LifeChartData Build() => new(nodes, edges, goals, [], entries, allocations);

    public ProgressionResult Compute() => ProgressionCalculator.Compute(Build(), Today, TimeZoneInfo.Utc);

    public NodeState StateOf(SkillNode node) => Compute().Nodes[node.Id].State;

    private void AddAllocation(SkillNode node, AllocationAction action) =>
        allocations.Add(new Allocation { NodeId = node.Id, Action = action, Timestamp = At(-1).AddMinutes(sequence++) });

    private static DateTimeOffset At(int dayOffset) =>
        new DateTimeOffset(Today.ToDateTime(new TimeOnly(9, 0)), TimeSpan.Zero).AddDays(dayOffset);
}
