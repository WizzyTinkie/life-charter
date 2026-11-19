using LifeCharter.Core.Common.Models;
using LifeCharter.Core.Goals.Models;
using LifeCharter.Core.Journal.Models;
using LifeCharter.Core.Progression.Business;
using LifeCharter.Core.Progression.Models;
using LifeCharter.Core.Retention.Models;
using LifeCharter.Core.Siege.Business;
using LifeCharter.Core.Siege.Models;
using LifeCharter.Core.SkillTree.Models;

namespace LifeCharter.Core.Tests.Siege.Business;

/// <summary>
/// A chart whose nodes are all mastered the day before the siege starts (Monday 2026-01-05),
/// plus helpers to log practice and to simulate a behaviour day by day. UTC throughout.
/// </summary>
internal sealed class SiegeScenario
{
    public static readonly DateOnly Start = new(2026, 1, 5);

    private readonly List<SkillNode> nodes = [];
    private readonly List<SkillEdge> edges = [];
    private readonly List<Goal> goals = [];
    private readonly List<LogEntry> entries = [];
    private readonly List<Allocation> allocations = [];
    private readonly List<HabitCheck> checks = [];
    private readonly List<WardUse> wardUses = [];
    private readonly List<Vacation> vacations = [];
    private int sequence;

    public SiegeScenario(Difficulty difficulty = Difficulty.Standard, bool started = true)
    {
        Root = new SkillNode { Kind = NodeKind.Root, Title = "You", RetentionModel = RetentionModel.Milestone };
        nodes.Add(Root);
        DifficultyPresets.Apply(Settings, difficulty);
        Settings.StartDate = started ? Start : null;
    }

    public SkillNode Root { get; }

    public SiegeSettings Settings { get; } = new();

    public IReadOnlyList<SkillNode> Nodes => nodes.Skip(1).ToList();

    /// <summary>
    /// A node mastered on the day before the start. Habit: a daily goal. Capacity: two sessions a
    /// week (dose 2, due every 3 days). Memory, Motor skill, Milestone: a milestone goal.
    /// </summary>
    public SkillNode Mastered(string title, RetentionModel model, LifeAttribute attribute = LifeAttribute.Health, Complexity complexity = Complexity.Standard)
    {
        var node = new SkillNode { Title = title, Attribute = attribute, RetentionModel = model, Complexity = complexity, XpReward = 50 };
        nodes.Add(node);
        edges.Add(new SkillEdge { FromNodeId = Root.Id, ToNodeId = node.Id, Kind = EdgeKind.Path });

        var goal = model switch
        {
            RetentionModel.Habit => new Goal { Metric = GoalMetric.Count, Window = GoalWindow.Daily, Target = 1, Periods = 1 },
            RetentionModel.Capacity => new Goal { Metric = GoalMetric.Count, Window = GoalWindow.Weekly, Target = 2, Periods = 1 },
            _ => new Goal { Metric = GoalMetric.Milestone },
        };
        goal.NodeId = node.Id;
        goal.StartDate = Start.AddDays(-30);
        goals.Add(goal);

        var masteredOn = Start.AddDays(-1);
        allocations.Add(new Allocation { NodeId = node.Id, Action = AllocationAction.Allocate, Timestamp = At(masteredOn, 8) });
        Log(node, masteredOn);
        if (model == RetentionModel.Capacity)
            Log(node, masteredOn);

        return node;
    }

    public void Log(SkillNode node, DateOnly day) =>
        entries.Add(new LogEntry { NodeId = node.Id, Amount = 1, Timestamp = At(day, 18) });

    public void Review(SkillNode node, DateOnly day, ReviewRating rating = ReviewRating.Good) =>
        entries.Add(new LogEntry { NodeId = node.Id, Amount = 1, Unit = "recall", Rating = rating, Timestamp = At(day, 19) });

    /// <summary>Does what defends the node: a recall for Memory nodes, a log entry otherwise.</summary>
    public void Defend(SkillNode node, DateOnly day)
    {
        if (node.RetentionModel == RetentionModel.Memory)
            Review(node, day);
        else
            Log(node, day);
    }

    public void SelfCheck(SkillNode node, DateOnly day, int answer) =>
        checks.Add(new HabitCheck
        {
            NodeId = node.Id,
            Timestamp = At(day, 20),
            Automatically = answer,
            WithoutRemembering = answer,
            WithoutThinking = answer,
            BeforeRealizing = answer,
        });

    public void Ward(DateOnly day) => wardUses.Add(new WardUse { Date = day });

    public void Vacation(DateOnly from, DateOnly to) => vacations.Add(new Vacation { StartDate = from, EndDate = to });

    public LifeChartData Build() =>
        new(nodes, edges, goals, [], entries, allocations)
        {
            HabitChecks = checks,
            WardUses = wardUses,
            Vacations = vacations,
            SiegeSettings = Settings,
        };

    public ProgressionResult Compute(DateOnly today) => ProgressionCalculator.Compute(Build(), today, TimeZoneInfo.Utc);

    public NodeRetention Retention(SkillNode node, DateOnly today) => Compute(today).Siege.Nodes[node.Id];

    /// <summary>
    /// Runs <paramref name="days"/> days from the start. Each morning the behaviour sees that
    /// day's attacks and adds whatever practice it does; returns the result on the day after.
    /// </summary>
    public ProgressionResult Simulate(int days, Action<int, DateOnly, SiegeResult> behave)
    {
        for (var index = 0; index < days; index++)
        {
            var day = Start.AddDays(index);
            behave(index, day, Compute(day).Siege);
        }

        return Compute(Start.AddDays(days));
    }

    private DateTimeOffset At(DateOnly day, int hour) =>
        new DateTimeOffset(day.ToDateTime(new TimeOnly(hour, 0)), TimeSpan.Zero).AddSeconds(sequence++);
}
