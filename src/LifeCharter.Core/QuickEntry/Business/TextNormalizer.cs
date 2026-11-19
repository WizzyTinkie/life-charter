using System.Globalization;
using System.Text;

namespace LifeCharter.Core.QuickEntry.Business;

public static class TextNormalizer
{
    /// <summary>Lower case without diacritics, so "Čeština" matches "cestina".</summary>
    public static string Normalize(string text)
    {
        var decomposed = text.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                builder.Append(character);
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    public static string[] Words(string text) =>
        Normalize(text).Split([' ', '\t', '-', '/'], StringSplitOptions.RemoveEmptyEntries);
}
