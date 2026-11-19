namespace LifeCharter.Core.Retention.Models;

/// <summary>What was done for one node on one day: plain log entries, recall ratings and self-checks.</summary>
public sealed record DayPractice(int Entries, IReadOnlyList<ReviewRating> Ratings, IReadOnlyList<HabitCheck> Checks)
{
    public static DayPractice None { get; } = new(0, [], []);

    public bool IsEmpty => Entries == 0 && Ratings.Count == 0;
}
