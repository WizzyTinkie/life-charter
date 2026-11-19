namespace LifeCharter.Core.QuickEntry.Business;

/// <summary>Spellings of common units (English and Czech) and the conversions between them.</summary>
public static class UnitAliases
{
    private static readonly Dictionary<string, string> Canonical = new()
    {
        ["l"] = "l", ["liter"] = "l", ["liters"] = "l", ["litre"] = "l", ["litres"] = "l", ["litr"] = "l", ["litru"] = "l", ["litry"] = "l",
        ["ml"] = "ml",
        ["min"] = "min", ["mins"] = "min", ["minute"] = "min", ["minutes"] = "min", ["minut"] = "min", ["minuty"] = "min",
        ["h"] = "h", ["hr"] = "h", ["hrs"] = "h", ["hour"] = "h", ["hours"] = "h", ["hod"] = "h", ["hodin"] = "h", ["hodiny"] = "h", ["hodina"] = "h",
        ["km"] = "km", ["k"] = "km",
        ["page"] = "pages", ["pages"] = "pages", ["p"] = "pages", ["str"] = "pages", ["strana"] = "pages", ["strany"] = "pages", ["stran"] = "pages",
        ["step"] = "steps", ["steps"] = "steps", ["krok"] = "steps", ["kroky"] = "steps", ["kroku"] = "steps",
        ["session"] = "session", ["sessions"] = "session", ["x"] = "session", ["times"] = "session", ["krat"] = "session",
    };

    public static bool IsKnown(string normalizedWord) => Canonical.ContainsKey(normalizedWord);

    public static string Canonicalize(string unit)
    {
        var normalized = TextNormalizer.Normalize(unit);
        return Canonical.GetValueOrDefault(normalized, normalized);
    }

    /// <summary>The factor that converts an amount in <paramref name="from"/> into <paramref name="to"/>; null when they don't convert.</summary>
    public static double? Factor(string from, string to)
    {
        var source = Canonicalize(from);
        var target = Canonicalize(to);
        if (source == target)
            return 1;

        return (source, target) switch
        {
            ("h", "min") => 60,
            ("min", "h") => 1 / 60.0,
            ("l", "ml") => 1000,
            ("ml", "l") => 0.001,
            _ => null,
        };
    }
}
