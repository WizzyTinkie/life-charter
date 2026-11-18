namespace LifeCharter.Platform.DesktopWidget.Business;

/// <summary>Minimal reader for KDE's INI-style config files (groups, keys, backslash escapes).</summary>
public static class KConfigFile
{
    public static IReadOnlyDictionary<string, Dictionary<string, string>> Parse(IEnumerable<string> lines)
    {
        var groups = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        Dictionary<string, string>? current = null;

        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;

            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                var name = line[1..^1];
                if (!groups.TryGetValue(name, out current))
                {
                    current = new Dictionary<string, string>(StringComparer.Ordinal);
                    groups[name] = current;
                }

                continue;
            }

            var separator = line.IndexOf('=');
            if (current is null || separator <= 0)
                continue;

            current[line[..separator].Trim()] = Unescape(line[(separator + 1)..]);
        }

        return groups;
    }

    /// <summary>KConfig writes a backslash as <c>\\</c>; this is the only escape our values use.</summary>
    private static string Unescape(string value) => value.Replace(@"\\", @"\", StringComparison.Ordinal);
}
