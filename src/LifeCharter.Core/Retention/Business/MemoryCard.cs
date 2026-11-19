using FSRS.Core.Enums;
using FSRS.Core.Models;
using FSRS.Core.Services;
using LifeCharter.Core.Retention.Models;

namespace LifeCharter.Core.Retention.Business;

/// <summary>
/// FSRS state of one node, reviewed at most once a day. Every review happens at noon UTC of its
/// day, so intervals are whole days and both devices compute the same schedule.
/// </summary>
internal sealed class MemoryCard
{
    private readonly Scheduler scheduler;
    private Card card;

    public MemoryCard(double desiredRetention, Card? initial = null)
        : this(new Scheduler(desiredRetention: desiredRetention, enableFuzzing: false), initial ?? new Card())
    {
    }

    private MemoryCard(Scheduler scheduler, Card card)
    {
        this.scheduler = scheduler;
        this.card = card;
    }

    public bool IsStarted => card.LastReview is not null;

    public double Stability => card.Stability ?? 0;

    public DateOnly DueDay => DateOnly.FromDateTime(card.Due);

    /// <summary>A card that starts out already learned, as if reviewed on <paramref name="day"/>.</summary>
    public static Card Seeded(DateOnly day, double stabilityDays) =>
        new(null, State.Review, null, stabilityDays, 5, Noon(day.AddDays((int)stabilityDays)), Noon(day));

    public void Review(DateOnly day, ReviewRating rating) =>
        (card, _) = scheduler.ReviewCard(card, ToFsrs(rating), Noon(day));

    public double Retrievability(DateOnly day) => scheduler.GetCardRetrievability(card, Noon(day));

    public MemoryCard Clone() => new(scheduler, card.Clone());

    private static DateTime Noon(DateOnly day) => day.ToDateTime(new TimeOnly(12, 0), DateTimeKind.Utc);

    private static Rating ToFsrs(ReviewRating rating) => rating switch
    {
        ReviewRating.Again => Rating.Again,
        ReviewRating.Hard => Rating.Hard,
        ReviewRating.Easy => Rating.Easy,
        _ => Rating.Good,
    };
}
