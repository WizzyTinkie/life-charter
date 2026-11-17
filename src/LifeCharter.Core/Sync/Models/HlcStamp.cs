using System.Globalization;

namespace LifeCharter.Core.Sync.Models;

/// <summary>
/// Timestamp of a hybrid logical clock. The string form sorts lexicographically in the same
/// order as the stamps themselves, so SQLite can compare <c>updated_at</c> columns as text.
/// </summary>
public readonly record struct HlcStamp(long PhysicalMs, int Counter, string DeviceId) : IComparable<HlcStamp>
{
    public int CompareTo(HlcStamp other)
    {
        var byTime = PhysicalMs.CompareTo(other.PhysicalMs);
        if (byTime != 0) return byTime;
        var byCounter = Counter.CompareTo(other.Counter);
        return byCounter != 0 ? byCounter : string.CompareOrdinal(DeviceId, other.DeviceId);
    }

    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"{PhysicalMs:D15}-{Counter:D5}-{DeviceId}");

    public static HlcStamp Parse(string value)
    {
        var parts = value.Split('-', 3);
        if (parts.Length != 3)
            throw new FormatException($"Not an HLC stamp: '{value}'.");

        return new HlcStamp(
            long.Parse(parts[0], CultureInfo.InvariantCulture),
            int.Parse(parts[1], CultureInfo.InvariantCulture),
            parts[2]);
    }

    public static bool operator <(HlcStamp left, HlcStamp right) => left.CompareTo(right) < 0;
    public static bool operator >(HlcStamp left, HlcStamp right) => left.CompareTo(right) > 0;
    public static bool operator <=(HlcStamp left, HlcStamp right) => left.CompareTo(right) <= 0;
    public static bool operator >=(HlcStamp left, HlcStamp right) => left.CompareTo(right) >= 0;
}
