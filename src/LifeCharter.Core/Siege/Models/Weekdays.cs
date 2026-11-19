namespace LifeCharter.Core.Siege.Models;

/// <summary>A set of weekdays, for example the rest days without attacks.</summary>
[Flags]
public enum Weekdays
{
    None = 0,
    Monday = 1,
    Tuesday = 2,
    Wednesday = 4,
    Thursday = 8,
    Friday = 16,
    Saturday = 32,
    Sunday = 64,
}
