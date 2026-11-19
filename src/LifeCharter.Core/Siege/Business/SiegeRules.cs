using LifeCharter.Core.Siege.Models;

namespace LifeCharter.Core.Siege.Business;

/// <summary>Tunable numbers of the daily siege (PLAN.md section 5).</summary>
public static class SiegeRules
{
    public const int MaxIntegrity = 100;

    /// <summary>Damage of an undefended attack at severity 1 and multiplier 1: about five misses in a row corrupt a node.</summary>
    public const double BaseDamage = 20;

    /// <summary>Fortified (and Seasoned) nodes take half damage.</summary>
    public const double FortifiedDamageFactor = 0.5;

    /// <summary>Fully defended days that earn one ward.</summary>
    public const int DaysPerWard = 7;

    public const int StartingWards = 1;

    public static int Damage(double severity, bool fortified, double multiplier) =>
        (int)Math.Round(BaseDamage * severity * multiplier * (fortified ? FortifiedDamageFactor : 1));

    public static bool IsRestDay(Weekdays restDays, DateOnly day) =>
        restDays.HasFlag(day.DayOfWeek switch
        {
            DayOfWeek.Monday => Weekdays.Monday,
            DayOfWeek.Tuesday => Weekdays.Tuesday,
            DayOfWeek.Wednesday => Weekdays.Wednesday,
            DayOfWeek.Thursday => Weekdays.Thursday,
            DayOfWeek.Friday => Weekdays.Friday,
            DayOfWeek.Saturday => Weekdays.Saturday,
            _ => Weekdays.Sunday,
        });
}
