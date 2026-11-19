using LifeCharter.Core.Siege.Models;
using LifeCharter.Core.SkillTree.Models;

namespace LifeCharter.Core.Tests.Siege.Business;

public sealed class SiegeEngineTests
{
    private static readonly DateOnly Start = SiegeScenario.Start;

    [Fact]
    public void Nothing_is_attacked_before_the_siege_is_switched_on()
    {
        var scenario = new SiegeScenario(started: false);
        scenario.Mastered("Water", RetentionModel.Habit);

        var siege = scenario.Compute(Start.AddDays(3)).Siege;

        Assert.False(siege.IsStarted);
        Assert.Empty(siege.Today.Attacks);
    }

    [Fact]
    public void Due_nodes_are_attacked_and_defense_is_shown_during_the_day()
    {
        var scenario = new SiegeScenario();
        var water = scenario.Mastered("Water", RetentionModel.Habit);
        var czech = scenario.Mastered("Czech", RetentionModel.Memory, LifeAttribute.Might);

        Assert.Equal(2, scenario.Compute(Start).Siege.Today.Attacks.Count);

        scenario.Log(water, Start);
        var today = scenario.Compute(Start).Siege.Today;
        Assert.True(today.Attacks.Single(attack => attack.NodeId == water.Id).Defended);
        Assert.False(today.Attacks.Single(attack => attack.NodeId == czech.Id).Defended);
    }

    [Fact]
    public void Daily_budget_caps_attacks_and_postponed_nodes_take_no_damage()
    {
        var scenario = new SiegeScenario();
        scenario.Settings.AttacksPerDay = 3;
        for (var i = 0; i < 8; i++)
            scenario.Mastered($"Topic {i}", RetentionModel.Memory, LifeAttribute.Might);

        var result = scenario.Compute(Start.AddDays(1));

        Assert.Equal(3, result.Siege.Yesterday!.Attacks.Count);
        Assert.Equal(3, result.Siege.Nodes.Values.Count(node => node.Integrity < 100));
    }

    [Fact]
    public void Rest_day_has_no_attacks()
    {
        var scenario = new SiegeScenario();
        scenario.Mastered("Water", RetentionModel.Habit);

        var sunday = scenario.Compute(Start.AddDays(6)).Siege.Today;

        Assert.Equal(SiegeDayKind.Rest, sunday.Kind);
        Assert.Empty(sunday.Attacks);
    }

    [Fact]
    public void Five_missed_days_corrupt_a_habit_and_withhold_its_xp_until_cleansed()
    {
        var scenario = new SiegeScenario();
        var water = scenario.Mastered("Water", RetentionModel.Habit);
        var before = scenario.Compute(Start).Character.TotalXp;

        Assert.Equal(8, scenario.Retention(water, Start.AddDays(4)).Integrity);

        var corrupted = scenario.Compute(Start.AddDays(5));
        Assert.Equal(NodeState.Corrupted, corrupted.Nodes[water.Id].State);
        Assert.Equal(before - water.XpReward, corrupted.Character.TotalXp);

        scenario.Log(water, Start.AddDays(5));
        scenario.Log(water, Start.AddDays(6));
        Assert.True(scenario.Retention(water, Start.AddDays(6)).IsCorrupted);

        scenario.Log(water, Start.AddDays(7));
        var cleansed = scenario.Compute(Start.AddDays(7));
        Assert.Equal(NodeState.Mastered, cleansed.Nodes[water.Id].State);
        Assert.Equal(100, cleansed.Siege.Nodes[water.Id].Integrity);
        Assert.Equal(1, cleansed.Siege.Nodes[water.Id].TimesCorrupted);
    }

    [Fact]
    public void Casual_dims_a_node_but_keeps_its_xp()
    {
        var scenario = new SiegeScenario(Difficulty.Casual);
        scenario.Settings.RestDays = Weekdays.None;
        var water = scenario.Mastered("Water", RetentionModel.Habit);
        var before = scenario.Compute(Start).Character.TotalXp;

        var result = scenario.Compute(Start.AddDays(20));

        Assert.True(result.Siege.Nodes[water.Id].IsCorrupted);
        Assert.False(result.Siege.Nodes[water.Id].RewardWithheld);
        Assert.Equal(before, result.Character.TotalXp);
    }

    [Fact]
    public void Defending_heals_a_node_to_full_integrity()
    {
        var scenario = new SiegeScenario();
        var water = scenario.Mastered("Water", RetentionModel.Habit);

        Assert.Equal(58, scenario.Retention(water, Start.AddDays(2)).Integrity);

        scenario.Log(water, Start.AddDays(2));
        Assert.Equal(100, scenario.Retention(water, Start.AddDays(3)).Integrity);
    }

    [Fact]
    public void Ward_shields_a_whole_day()
    {
        var scenario = new SiegeScenario();
        var water = scenario.Mastered("Water", RetentionModel.Habit);
        scenario.Ward(Start);

        var result = scenario.Compute(Start.AddDays(1));

        Assert.True(result.Siege.Yesterday!.Shielded);
        Assert.Equal(100, result.Siege.Nodes[water.Id].Integrity);
        Assert.Equal(0, result.Siege.Wards);
    }

    [Fact]
    public void Seven_fully_defended_days_earn_a_ward()
    {
        var scenario = new SiegeScenario();
        var water = scenario.Mastered("Water", RetentionModel.Habit);

        // Sunday is a rest day, so seven defended siege days end on the eighth day.
        var result = scenario.Simulate(8, (_, day, _) => scenario.Log(water, day));

        Assert.Equal(7, result.Siege.Streak);
        Assert.Equal(2, result.Siege.Wards);
    }

    [Fact]
    public void Engraved_node_is_never_attacked()
    {
        var scenario = new SiegeScenario();
        var marathon = scenario.Mastered("Marathon", RetentionModel.Milestone, LifeAttribute.Stamina);

        var result = scenario.Compute(Start.AddDays(30));

        Assert.Equal(NodeState.Engraved, result.Nodes[marathon.Id].State);
        Assert.Empty(result.Siege.Today.Attacks);
    }

    [Fact]
    public void Peaceful_shows_attacks_but_deals_no_damage()
    {
        var scenario = new SiegeScenario(Difficulty.Peaceful);
        var water = scenario.Mastered("Water", RetentionModel.Habit);

        var result = scenario.Compute(Start.AddDays(30));

        Assert.NotEmpty(result.Siege.Today.Attacks);
        Assert.Equal(100, result.Siege.Nodes[water.Id].Integrity);
    }

    [Fact]
    public void Capacity_takes_damage_only_after_two_weeks_without_a_session()
    {
        var scenario = new SiegeScenario();
        scenario.Settings.RestDays = Weekdays.None;
        var cardio = scenario.Mastered("Cardio", RetentionModel.Capacity, LifeAttribute.Stamina);

        Assert.Equal(100, scenario.Retention(cardio, Start.AddDays(14)).Integrity);
        Assert.True(scenario.Retention(cardio, Start.AddDays(15)).Integrity < 100);
    }
}
