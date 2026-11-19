using LifeCharter.Core.Retention.Interfaces;
using LifeCharter.Core.Retention.Models;
using LifeCharter.Core.SkillTree.Models;

namespace LifeCharter.Core.Retention.Business;

/// <summary>
/// Physical technique decays slowly. Mastering the goal is the proficiency test; the node then
/// follows FSRS from a high starting stability, and any practice counts as a Good review. It
/// engraves after a few sessions spaced over several weeks.
/// </summary>
internal sealed class MotorSkillTracker(DateOnly masteredOn, double desiredRetention) : IRetentionTracker
{
    private const int EstimateSpacingDays = 7;

    private readonly MemoryCard card =
        new(desiredRetention, MemoryCard.Seeded(masteredOn, RetentionRules.MotorInitialStabilityDays));

    private int sessions;
    private DateOnly? firstSession;
    private DateOnly? lastSession;
    private DateOnly? engravedOn;

    private int SpanDays => Span(firstSession, lastSession);

    public bool IsFortified => engravedOn is not null || sessions >= RetentionRules.MotorEngravedSessions / 2;

    public bool IsDue(DateOnly day) => engravedOn is null && day > masteredOn && card.DueDay <= day;

    public double Severity(DateOnly day) => RetentionRules.RecallSeverity(card.Retrievability(day), desiredRetention);

    public bool Defends(DayPractice practice) => !practice.IsEmpty;

    public void Advance(DateOnly day, DayPractice practice, bool paused)
    {
        if (day <= masteredOn || practice.IsEmpty)
            return;

        card.Review(day, practice.Ratings.Count > 0 ? practice.Ratings[0] : ReviewRating.Good);
        if (lastSession is not { } last || day.DayNumber - last.DayNumber >= RetentionRules.MotorSessionSpacingDays)
        {
            sessions++;
            firstSession ??= day;
            lastSession = day;
        }

        if (engravedOn is null && MeetsEngraveCriteria(sessions, SpanDays))
            engravedOn = day;
    }

    public void Miss(DateOnly day)
    {
    }

    public RetentionStatus Status(DateOnly today)
    {
        EngraveCriterion[] criteria =
        [
            new(EngraveCriterionKind.SpacedSessions, sessions, RetentionRules.MotorEngravedSessions),
            new(EngraveCriterionKind.SpanDays, SpanDays, RetentionRules.MotorEngravedSpanDays),
        ];

        var engraved = engravedOn is not null;
        return new RetentionStatus(
            RetentionModel.MotorSkill,
            IsFortified,
            engraved,
            engraved ? 1 : criteria.Average(criterion => criterion.Fraction),
            criteria,
            engraved ? null : Estimate(today));
    }

    /// <summary>When the node would engrave with one practice session a week from now on.</summary>
    private DateOnly Estimate(DateOnly today)
    {
        var count = sessions;
        var first = firstSession;
        var last = lastSession;
        var day = last is { } previous ? previous.AddDays(RetentionRules.MotorSessionSpacingDays) : masteredOn.AddDays(1);
        if (day < today)
            day = today;

        while (true)
        {
            count++;
            first ??= day;
            last = day;
            if (MeetsEngraveCriteria(count, Span(first, last)))
                return day;

            day = day.AddDays(EstimateSpacingDays);
        }
    }

    private static bool MeetsEngraveCriteria(int sessions, int spanDays) =>
        sessions >= RetentionRules.MotorEngravedSessions && spanDays >= RetentionRules.MotorEngravedSpanDays;

    private static int Span(DateOnly? first, DateOnly? last) =>
        first is { } from && last is { } to ? to.DayNumber - from.DayNumber : 0;
}
