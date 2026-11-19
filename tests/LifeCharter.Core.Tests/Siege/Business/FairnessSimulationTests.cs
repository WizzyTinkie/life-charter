using LifeCharter.Core.Progression.Models;
using LifeCharter.Core.Siege.Models;
using LifeCharter.Core.SkillTree.Models;

namespace LifeCharter.Core.Tests.Siege.Business;

/// <summary>
/// The acceptance criteria of PLAN.md section 5: a year of each behaviour profile on Standard.
/// The simulated user sees the morning's attacks and defends some of them.
/// </summary>
public sealed class FairnessSimulationTests
{
    private const int Year = 365;

    private readonly SiegeScenario scenario = new();
    private readonly SkillNode water;
    private readonly SkillNode czech;
    private readonly SkillNode cardio;
    private readonly SkillNode swimming;

    public FairnessSimulationTests()
    {
        water = scenario.Mastered("Water", RetentionModel.Habit);
        czech = scenario.Mastered("Czech", RetentionModel.Memory, LifeAttribute.Might);
        cardio = scenario.Mastered("Cardio", RetentionModel.Capacity, LifeAttribute.Stamina);
        swimming = scenario.Mastered("Swimming", RetentionModel.MotorSkill, LifeAttribute.Stamina);
    }

    [Fact]
    public void Eighty_percent_adherence_never_corrupts_a_node()
    {
        var result = scenario.Simulate(Year, (index, day, siege) =>
        {
            foreach (var (attack, position) in siege.Today.Attacks.Select((attack, position) => (attack, position)))
            {
                if ((index + position) % 5 != 0)
                    scenario.Defend(Node(attack.NodeId), day);
            }
        });

        Report(result);
        AssertNeverCorrupted(result);
    }

    [Fact]
    public void Two_week_vacation_loses_nothing()
    {
        var vacationStart = SiegeScenario.Start.AddDays(100);
        scenario.Vacation(vacationStart, vacationStart.AddDays(13));

        var result = scenario.Simulate(Year, (_, day, siege) =>
        {
            if (day >= vacationStart && day <= vacationStart.AddDays(13))
                return;

            DefendAll(day, siege);
        });

        Report(result);
        AssertNeverCorrupted(result);
        Assert.All(result.Siege.Nodes.Values, node => Assert.Equal(100, node.Integrity));
        Assert.Equal(NodeState.Fortified, result.Nodes[cardio.Id].State);
    }

    [Fact]
    public void A_few_sick_days_with_wards_cost_nothing()
    {
        var sickFrom = SiegeScenario.Start.AddDays(30);
        var sickDays = Enumerable.Range(0, 3).Select(sickFrom.AddDays).ToHashSet();

        var result = scenario.Simulate(40, (_, day, siege) =>
        {
            if (sickDays.Contains(day))
                scenario.Ward(day);
            else
                DefendAll(day, siege);
        });

        AssertNeverCorrupted(result);
        Assert.All(result.Siege.Nodes.Values, node => Assert.Equal(100, node.Integrity));
        Assert.True(result.Siege.Streak >= 30, $"Streak {result.Siege.Streak}: shielded days must not break it.");
    }

    [Fact]
    public void Training_only_on_weekends_survives_with_two_rest_days()
    {
        scenario.Settings.RestDays = Weekdays.Tuesday | Weekdays.Thursday;

        var result = scenario.Simulate(Year, (_, day, _) =>
        {
            if (day.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
                return;

            foreach (var node in scenario.Nodes)
                scenario.Defend(node, day);
        });

        Report(result);
        AssertNeverCorrupted(result);
    }

    [Fact]
    public void Perfect_adherence_engraves_memory_within_a_year_and_a_habit_within_three_months()
    {
        DateOnly? habitEngraved = null;
        DateOnly? memoryEngraved = null;

        scenario.Simulate(Year, (_, day, siege) =>
        {
            DefendAll(day, siege);
            scenario.Log(water, day);

            if (siege.Nodes[water.Id].Status.NeedsSelfCheck)
                scenario.SelfCheck(water, day, 5);

            habitEngraved ??= siege.Nodes[water.Id].Status.IsEngraved ? day : null;
            memoryEngraved ??= siege.Nodes[czech.Id].Status.IsEngraved ? day : null;
        });

        TestContext.Current.TestOutputHelper?.WriteLine($"Habit engraved {habitEngraved}, memory engraved {memoryEngraved}");
        Assert.NotNull(habitEngraved);
        Assert.NotNull(memoryEngraved);
        Assert.InRange(habitEngraved.Value.DayNumber - SiegeScenario.Start.DayNumber, 60, 100);
        Assert.InRange(memoryEngraved.Value.DayNumber - SiegeScenario.Start.DayNumber, 182, Year);
    }

    private void DefendAll(DateOnly day, SiegeResult siege)
    {
        foreach (var attack in siege.Today.Attacks)
            scenario.Defend(Node(attack.NodeId), day);
    }

    private SkillNode Node(Guid id) => scenario.Nodes.Single(node => node.Id == id);

    private static void AssertNeverCorrupted(ProgressionResult result) =>
        Assert.All(result.Siege.Nodes.Values, node => Assert.Equal(0, node.TimesCorrupted));

    private void Report(ProgressionResult result)
    {
        foreach (var node in scenario.Nodes)
        {
            var retention = result.Siege.Nodes[node.Id];
            TestContext.Current.TestOutputHelper?.WriteLine(
                $"{node.Title,-9} {result.Nodes[node.Id].State,-10} integrity {retention.Integrity,3} corrupted {retention.TimesCorrupted}× " +
                $"engrave {retention.Status.Fraction:P0} " +
                string.Join(", ", retention.Status.Criteria.Select(c => $"{c.Kind} {c.Current:0.#}/{c.Target:0.#}")));
        }

        TestContext.Current.TestOutputHelper?.WriteLine($"Wards {result.Siege.Wards}, streak {result.Siege.Streak}, best {result.Siege.BestStreak}");
    }
}
