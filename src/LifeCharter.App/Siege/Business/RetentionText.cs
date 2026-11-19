using System.Globalization;
using LifeCharter.Core.Retention.Models;
using LifeCharter.Core.Siege.Models;
using LifeCharter.Core.SkillTree.Models;

namespace LifeCharter.App.Siege.Business;

/// <summary>Short human-readable texts for retention and siege values.</summary>
public static class RetentionText
{
    private const double DaysPerMonth = 30.44;

    public static string State(NodeState state, RetentionModel model, ZeroIntegrityEffect effect) => state switch
    {
        NodeState.Fortified when model == RetentionModel.Capacity => "Seasoned",
        NodeState.Corrupted when effect == ZeroIntegrityEffect.Dim => "Faded",
        _ => state.ToString(),
    };

    public static string Model(RetentionModel model) => model switch
    {
        RetentionModel.Memory => "Memory: fades along the forgetting curve. Defend with a recall — recall first, then rate how it went.",
        RetentionModel.Habit => "Habit: becomes automatic with repetitions. Defend by doing it.",
        RetentionModel.Capacity => "Capacity: needs a weekly maintenance dose. Defend with a session; damage only after two weeks without one.",
        RetentionModel.MotorSkill => "Motor skill: technique fades slowly. Defend with a practice session.",
        _ => "Milestone: it happened. Engraved for good and never attacked.",
    };

    public static string ProgressTitle(RetentionStatus status) =>
        status.CanEngrave ? $"Engrave progress · {status.Fraction:P0}" : $"Seasoned progress · {status.Fraction:P0}";

    public static string Criterion(EngraveCriterion criterion)
    {
        var mark = criterion.IsMet ? "✓ " : "";
        return mark + criterion.Kind switch
        {
            EngraveCriterionKind.Stability => $"Memory stability {criterion.Current:0} / {criterion.Target:0} days",
            EngraveCriterionKind.Reviews => $"Successful recalls {criterion.Current:0} / {criterion.Target:0}",
            EngraveCriterionKind.SpanDays when criterion.Target >= 60 =>
                $"Spread over {criterion.Current / DaysPerMonth:0.#} / {criterion.Target / DaysPerMonth:0} months",
            EngraveCriterionKind.SpanDays => $"Spread over {criterion.Current:0} / {criterion.Target:0} days",
            EngraveCriterionKind.Repetitions => $"Repetitions {criterion.Current:0} / {criterion.Target:0}",
            EngraveCriterionKind.SelfCheck => criterion.Current <= 0
                ? $"Automaticity self-check: not taken (needs {criterion.Target:0}+ of 5)"
                : $"Automaticity self-check {criterion.Current:0.#} / {criterion.Target:0} of 5",
            EngraveCriterionKind.SpacedSessions => $"Spaced practice sessions {criterion.Current:0} / {criterion.Target:0}",
            EngraveCriterionKind.DaysMaintained => $"Kept up {criterion.Current:0} / {criterion.Target:0} days",
            EngraveCriterionKind.Adherence => $"Weeks with the maintenance dose {criterion.Current:P0} (needs {criterion.Target:P0})",
            _ => $"{criterion.Kind} {criterion.Current:0.#} / {criterion.Target:0.#}",
        };
    }

    public static string Estimate(RetentionStatus status) =>
        status.EstimatedDate is { } date
            ? $"{(status.CanEngrave ? "Engraved" : "Seasoned")} about {date.ToString("MMM yyyy", CultureInfo.CurrentCulture)} if you keep it up"
            : "";

    /// <summary>"−22 HP if undefended", "reminder · no damage yet" or "defended ✓".</summary>
    public static string Attack(Attack attack) =>
        attack.Defended ? "defended ✓"
        : attack.Threat > 0 ? $"−{attack.Threat} HP if undefended"
        : "reminder · no damage yet";

    public static string DayKind(SiegeDayKind kind) => kind switch
    {
        SiegeDayKind.Rest => "Rest day · no attacks",
        SiegeDayKind.Vacation => "On vacation · no attacks",
        SiegeDayKind.NotStarted => "The siege has not started",
        _ => "",
    };
}
