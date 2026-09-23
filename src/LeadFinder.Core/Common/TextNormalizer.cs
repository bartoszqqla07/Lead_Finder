using System.Globalization;
using System.Text;

namespace LeadFinder.Common;

public static class TextNormalizer
{
    /// <summary>
    /// Zamienia tekst na slug bez polskich znaków, np. "Bielsko-Biała " → "bielsko-biala".
    /// Używane do porównywania nazw kategorii i do nazw plików.
    /// </summary>
    public static string ToSlug(string text)
    {
        // "ł" nie ma rozkładu kanonicznego w Unicode (to osobna litera, nie "l" + znak diakrytyczny),
        // więc NFD jej nie obsłuży – trzeba ją zamienić ręcznie.
        var decomposed = text.Trim().ToLowerInvariant().Replace('ł', 'l').Normalize(NormalizationForm.FormD);

        var builder = new StringBuilder(decomposed.Length);
        var lastWasSeparator = true; // true na starcie = brak separatora na początku sluga
        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
                continue; // ogonki i kreski oddzielone przez NFD

            if (char.IsAsciiLetterOrDigit(ch))
            {
                builder.Append(ch);
                lastWasSeparator = false;
            }
            else if (!lastWasSeparator)
            {
                builder.Append('-');
                lastWasSeparator = true;
            }
        }

        return builder.ToString().TrimEnd('-');
    }
}
