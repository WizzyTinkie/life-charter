using LifeCharter.Core.Retention.Interfaces;
using LifeCharter.Core.Retention.Models;
using LifeCharter.Core.SkillTree.Models;

namespace LifeCharter.Core.Retention.Business;

/// <summary>
/// Knowledge fades along the forgetting curve. FSRS predicts recall; the node is due when recall
/// drops to the desired retention. Only a rated recall session defends it (testing effect), and
/// mastering the goal counts as the first learning session.
/// </summary>
internal sealed class MemoryTracker(DateOnly masteredOn, double desiredRetention) : IRetentionTracker
{
    private const int EstimateHorizonYears = 5;

    private readonly MemoryCard card = new(desiredRetention);
    private int successfulReviews;
    private DateOnly? firstSuccess;
    private DateOnly? lastSuccess;
    private DateOnly? engravedOn;

    private int SpanDays => Span(firstSuccess, lastSuccess);

    public bool IsFortified => engravedOn is not null || card.Stability >= RetentionRules.FortifiedStabilityDays;

    public bool IsDue(DateOnly day) => engravedOn is null && card.IsStarted && card.DueDay <= day;

    public double Severity(DateOnly day) => RetentionRules.RecallSeverity(card.Retrievability(day), desiredRetention);

    public bool Defends(DayPractice practice) => practice.Ratings.Count > 0;

    public void Advance(DateOnly day, DayPractice practice, bool paused)
    {
        if (practice.Ratings.Count > 0)
        {
            var rating = practice.Ratings[0];
            card.Review(day, rating);
            if (rating != ReviewRating.Again)
            {
                successfulReviews++;
                firstSuccess ??= day;
                lastSuccess = day;
            }
        }
        else if (day == masteredOn && !card.IsStarted)
        {
            card.Review(day, ReviewRating.Good);
        }

        if (engravedOn is null && MeetsEngraveCriteria(card.Stability, successfulReviews, SpanDays))
            engravedOn = day;
    }

    public void Miss(DateOnly day)
    {
        // Forgetting needs no bookkeeping: recall keeps falling until the next review.
    }

    public RetentionStatus Status(DateOnly today)
    {
        EngraveCriterion[] criteria =
        [
            new(EngraveCriterionKind.Stability, card.Stability, RetentionRules.EngravedStabilityDays),
            new(EngraveCriterionKind.Reviews, successfulReviews, RetentionRules.EngravedMinimumReviews),
            new(EngraveCriterionKind.SpanDays, SpanDays, RetentionRules.EngravedMinimumSpanDays),
        ];

        var engraved = engravedOn is not null;
        return new RetentionStatus(
            RetentionModel.Memory,
            IsFortified,
            engraved,
            engraved ? 1 : criteria.Average(criterion => criterion.Fraction),
            criteria,
            engraved ? null : Estimate(today));
    }

    /// <summary>When the node would engrave if every review from now on is on time and rated Good.</summary>
    private DateOnly? Estimate(DateOnly today)
    {
        var simulated = card.Clone();
        var reviews = successfulReviews;
        var first = firstSuccess;
        var last = lastSuccess;
        var day = simulated.IsStarted && simulated.DueDay > today ? simulated.DueDay : today;
        var horizon = today.AddYears(EstimateHorizonYears);

        while (day <= horizon)
        {
            simulated.Review(day, ReviewRating.Good);
            reviews++;
            first ??= day;
            last = day;
            if (MeetsEngraveCriteria(simulated.Stability, reviews, Span(first, last)))
                return day;

            day = simulated.DueDay > day ? simulated.DueDay : day.AddDays(1);
        }

        return null;
    }

    private static bool MeetsEngraveCriteria(double stability, int reviews, int spanDays) =>
        stability >= RetentionRules.EngravedStabilityDays &&
        reviews >= RetentionRules.EngravedMinimumReviews &&
        spanDays >= RetentionRules.EngravedMinimumSpanDays;

    private static int Span(DateOnly? first, DateOnly? last) =>
        first is { } from && last is { } to ? to.DayNumber - from.DayNumber : 0;
}
