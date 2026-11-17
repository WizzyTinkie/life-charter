using LifeCharter.Core.Sync.Models;

namespace LifeCharter.Core.Sync.Business;

/// <summary>
/// Hybrid logical clock: wall-clock milliseconds plus a counter, so stamps stay monotonic
/// on one device and causally ordered after merging another device's changes, even when
/// the two machines' clocks disagree.
/// </summary>
public sealed class HybridLogicalClock(string deviceId, TimeProvider? timeProvider = null)
{
    private const int MaxCounter = 99_999;

    private readonly TimeProvider time = timeProvider ?? TimeProvider.System;
    private readonly Lock gate = new();
    private long lastMs;
    private int counter;

    public string DeviceId { get; } = deviceId;

    public HlcStamp Now()
    {
        lock (gate)
        {
            var wall = time.GetUtcNow().ToUnixTimeMilliseconds();
            if (wall > lastMs)
            {
                lastMs = wall;
                counter = 0;
            }
            else
            {
                Increment();
            }

            return new HlcStamp(lastMs, counter, DeviceId);
        }
    }

    /// <summary>Advances the clock past a stamp received from another device.</summary>
    public void Observe(HlcStamp remote)
    {
        lock (gate)
        {
            var wall = time.GetUtcNow().ToUnixTimeMilliseconds();
            var max = Math.Max(wall, Math.Max(lastMs, remote.PhysicalMs));

            if (max == lastMs && max == remote.PhysicalMs)
                counter = Math.Max(counter, remote.Counter);
            else if (max == remote.PhysicalMs)
                counter = remote.Counter;
            else if (max != lastMs)
                counter = -1;

            lastMs = max;
            Increment();
        }
    }

    private void Increment()
    {
        if (counter >= MaxCounter)
        {
            lastMs++;
            counter = 0;
        }
        else
        {
            counter++;
        }
    }
}
