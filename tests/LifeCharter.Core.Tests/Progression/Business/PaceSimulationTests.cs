using System.Text;
using LifeCharter.Core.Common.Models;
using LifeCharter.Core.Goals.Models;
using LifeCharter.Core.Journal.Models;
using LifeCharter.Core.Progression.Business;
using LifeCharter.Core.Progression.Models;
using LifeCharter.Core.SkillTree.Business;
using LifeCharter.Core.SkillTree.Models;

namespace LifeCharter.Core.Tests.Progression.Business;

/// <summary>
/// Simulates a steady user on the starter tree to check that levels, skill points and
/// masteries arrive at a motivating pace. The timeline is written to the test output.
/// </summary>
public sealed class PaceSimulationTests
{
    private static readonly DateOnly Start = new(2026, 11, 16); // a Monday
    private static readonly string[] FirstPicks = ["Hydration", "Languages", "Cardio"];

    [Fact]
    public void Steady_routine_levels_up_in_the_first_week_and_never_stalls()
    {
        var timeline = Simulate(days: 60);
        TestContext.Current.TestOutputHelper?.WriteLine(timeline.Report);

        Assert.InRange(timeline.LevelOnDay[7], 2, 4);
        Assert.True(timeline.MasteredOnDay[21] >= 3, "At least three nodes mastered within three weeks.");
        Assert.Equal(16, timeline.MasteredOnDay[60]);
        Assert.True(timeline.LevelOnDay[60] >= 5, "Level 5 or more after two months.");
    }

    private static SimulationResult Simulate(int days)
    {
        var template = StarterTemplate.Create(Start);
        var entries = new List<LogEntry>();
        var allocations = new List<Allocation>();
        var levelOnDay = new Dictionary<int, int>();
        var masteredOnDay = new Dictionary<int, int>();
        var report = new StringBuilder("day  level  xp     points  training  mastered\n");

        LifeChartData Chart() => template with { Entries = entries, Allocations = allocations };

        foreach (var title in FirstPicks)
            Allocate(template.Nodes.Single(node => node.Title == title).Id, Start);

        for (var day = 0; day <= days; day++)
        {
            var date = Start.AddDays(day);
            var progression = ProgressionCalculator.Compute(Chart(), date, TimeZoneInfo.Utc);

            foreach (var goal in TrainingGoals(template, progression))
            {
                var amount = DailyAmount(goal, date, allocations.First(a => a.NodeId == goal.NodeId).Timestamp);
                if (amount > 0)
                    entries.Add(new LogEntry { NodeId = goal.NodeId, GoalId = goal.Id, Amount = amount, Unit = goal.Unit, Timestamp = At(date, 19) });
            }

            progression = ProgressionCalculator.Compute(Chart(), date, TimeZoneInfo.Utc);
            AllocateWhilePossible(template, progression, date);
            progression = ProgressionCalculator.Compute(Chart(), date, TimeZoneInfo.Utc);

            var character = progression.Character;
            var training = progression.Nodes.Values.Count(p => p.State == NodeState.Training);
            var mastered = progression.Nodes.Values.Count(p => p.State == NodeState.Mastered) - 1;
            levelOnDay[day] = character.Level.Level;
            masteredOnDay[day] = mastered;

            if (day % 7 == 0 || day is 3 or 60)
                report.AppendLine($"{day,3}  {character.Level.Level,5}  {character.TotalXp,5}  {character.SkillPoints,6}  {training,8}  {mastered,8}");
        }

        return new SimulationResult(report.ToString(), levelOnDay, masteredOnDay);

        void Allocate(Guid nodeId, DateOnly date) =>
            allocations.Add(new Allocation { NodeId = nodeId, Action = AllocationAction.Allocate, Timestamp = At(date, 20).AddSeconds(allocations.Count) });

        void AllocateWhilePossible(LifeChartData chart, ProgressionResult progression, DateOnly date)
        {
            foreach (var node in chart.Nodes.Where(node => node.Kind != NodeKind.Root))
            {
                if (progression.Character.SkillPoints <= 0)
                    return;

                if (AllocationRules.Check(node.Id, Chart(), progression) != AllocationBlock.None)
                    continue;

                Allocate(node.Id, date);
                progression = ProgressionCalculator.Compute(Chart(), date, TimeZoneInfo.Utc);
            }
        }
    }

    private static IEnumerable<Goal> TrainingGoals(LifeChartData chart, ProgressionResult progression) =>
        chart.Goals.Where(goal => progression.Nodes[goal.NodeId].State == NodeState.Training);

    /// <summary>What a steady user logs for a goal on a given day.</summary>
    private static double DailyAmount(Goal goal, DateOnly date, DateTimeOffset allocatedAt)
    {
        var weekday = date.DayOfWeek;
        var trainingDays = weekday is DayOfWeek.Monday or DayOfWeek.Wednesday or DayOfWeek.Friday;
        return goal.Metric switch
        {
            GoalMetric.Milestone => date.DayNumber - DateOnly.FromDateTime(allocatedAt.UtcDateTime).DayNumber == 20 ? 1 : 0,
            _ when goal.Window == GoalWindow.Daily => goal.Metric == GoalMetric.Sum ? goal.Target : 1,
            _ when goal.Window == GoalWindow.Weekly => trainingDays ? (goal.Metric == GoalMetric.Sum ? Math.Ceiling(goal.Target / 3) : 1) : 0,
            _ => 20, // Total goals: 20 pages a day.
        };
    }

    private static DateTimeOffset At(DateOnly date, int hour) =>
        new(date.ToDateTime(new TimeOnly(hour, 0)), TimeSpan.Zero);

    private sealed record SimulationResult(string Report, IReadOnlyDictionary<int, int> LevelOnDay, IReadOnlyDictionary<int, int> MasteredOnDay);
}
