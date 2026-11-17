using FSRS.Core.Constants;
using FSRS.Core.Enums;
using FSRS.Core.Models;
using FSRS.Core.Services;

namespace LifeCharter.Core.Tests.Retention.Business;

/// <summary>
/// Spike (e): checks which FSRS version FSRS.Core implements and how many on-time reviews
/// it takes for a Memory node to reach the "Engraved" threshold (stability ≥ 365 days).
/// </summary>
public sealed class FsrsSpikeTests
{
    private const double EngravedStabilityDays = 365;

    [Fact]
    public void Default_parameters_are_fsrs6()
    {
        // FSRS-4.5 has 17 weights, FSRS-5 has 19, FSRS-6 has 21 (trainable decay).
        Assert.Equal(21, FsrsConstants.DefaultParameters.Length);
    }

    [Theory]
    [InlineData(Rating.Good)]
    [InlineData(Rating.Easy)]
    public void On_time_reviews_reach_engraved_stability(Rating rating)
    {
        var run = ReviewUntilEngraved(rating, hardEvery: 0);
        TestContext.Current.TestOutputHelper?.WriteLine($"{rating}: {run.Reviews} reviews over {run.Days:F0} days\n{run.Trace}");

        Assert.InRange(run.Reviews, 3, 12);
        Assert.InRange(run.Days, 60, 730);
    }

    [Fact]
    public void Occasional_hard_recall_still_engraves_within_two_years()
    {
        var run = ReviewUntilEngraved(Rating.Good, hardEvery: 3);
        TestContext.Current.TestOutputHelper?.WriteLine($"Good with every 3rd Hard: {run.Reviews} reviews over {run.Days:F0} days\n{run.Trace}");

        Assert.InRange(run.Days, 60, 730);
    }

    private static EngraveRun ReviewUntilEngraved(Rating rating, int hardEvery)
    {
        var scheduler = new Scheduler(desiredRetention: 0.9, enableFuzzing: false);
        var start = new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc);
        var card = new Card();
        var now = start;
        var reviews = 0;
        var trace = new System.Text.StringBuilder();

        // Node-level reviews happen at most once a day, so same-day learning steps collapse
        // into the first session.
        while ((card.Stability ?? 0) < EngravedStabilityDays && reviews < 50)
        {
            reviews++;
            var thisRating = hardEvery > 0 && reviews % hardEvery == 0 ? Rating.Hard : rating;
            (card, _) = scheduler.ReviewCard(card, thisRating, now);
            trace.AppendLine($"  #{reviews} day {(now - start).TotalDays,6:F1} {thisRating,-5} state={card.State,-10} S={card.Stability,7:F1}d next=+{(card.Due - now).TotalDays:F1}d");
            now = card.Due > now.AddDays(1) ? card.Due : now.AddDays(1);
        }

        return new EngraveRun(reviews, (card.LastReview!.Value - start).TotalDays, trace.ToString());
    }

    private sealed record EngraveRun(int Reviews, double Days, string Trace);
}
