using LifeCharter.Core.Sync.Business;
using LifeCharter.Core.Sync.Models;

namespace LifeCharter.Core.Tests.Sync.Business;

public sealed class HybridLogicalClockTests
{
    private readonly TimeProvider time = Substitute.For<TimeProvider>();

    [Fact]
    public void Stamps_are_monotonic_when_the_wall_clock_stalls_or_goes_back()
    {
        var clock = new HybridLogicalClock("linux", time);

        SetWall(1_000);
        var first = clock.Now();
        var second = clock.Now();
        SetWall(900);
        var third = clock.Now();

        Assert.True(first < second);
        Assert.True(second < third);
        Assert.Equal(1_000, third.PhysicalMs);
    }

    [Fact]
    public void Observing_a_remote_stamp_from_the_future_moves_the_clock_past_it()
    {
        var clock = new HybridLogicalClock("linux", time);
        SetWall(1_000);
        var remote = new HlcStamp(5_000, 7, "windows");

        clock.Observe(remote);
        var next = clock.Now();

        Assert.True(next > remote);
        Assert.Equal(5_000, next.PhysicalMs);
    }

    [Fact]
    public void Text_form_sorts_like_the_stamps()
    {
        HlcStamp[] stamps =
        [
            new(2_000, 0, "a"),
            new(1_000, 12, "b"),
            new(1_000, 3, "z"),
            new(1_000, 3, "a"),
        ];

        var byValue = stamps.Order().Select(s => s.ToString());
        var byText = stamps.Select(s => s.ToString()).Order(StringComparer.Ordinal);

        Assert.Equal(byValue, byText);
    }

    [Fact]
    public void Parse_round_trips_device_ids_that_contain_dashes()
    {
        var stamp = new HlcStamp(1_731_800_000_000, 42, "0193a7-cachyos");

        Assert.Equal(stamp, HlcStamp.Parse(stamp.ToString()));
    }

    private void SetWall(long unixMs) =>
        time.GetUtcNow().Returns(DateTimeOffset.FromUnixTimeMilliseconds(unixMs));
}
