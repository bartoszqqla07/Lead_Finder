using System.Text.RegularExpressions;

namespace LeadFinder.Services;

/// <summary>Sygnały, że strona jest przestarzała – wyciągane z HTML strony głównej.</summary>
/// <param name="IsMobileFriendly">Czy strona ma meta viewport (bez niego telefon pokazuje pomniejszoną wersję desktopową).</param>
/// <param name="CopyrightYear">Najnowszy rok ze stopki ("© 2015–2019" → 2019); null, gdy nie znaleziono.</param>
public sealed partial record PageSignals(bool IsMobileFriendly, int? CopyrightYear)
{
    /// <summary>Analizuje HTML strony głównej.</summary>
    public static PageSignals Detect(string html) =>
        new(ViewportRegex().IsMatch(html), FindCopyrightYear(html));

    /// <summary>
    /// Szuka lat przy "©", "&amp;copy;" albo "copyright" i zwraca najnowszy sensowny. Strony, które wstawiają
    /// rok przez JavaScript, nie mają go w HTML – wtedy wynik to null (brak sygnału, a nie "stara strona").
    /// </summary>
    private static int? FindCopyrightYear(string html)
    {
        var currentYear = DateTime.Now.Year;
        int? latest = null;

        foreach (Match match in CopyrightRegex().Matches(html))
        {
            foreach (var group in new[] { match.Groups["from"], match.Groups["to"] })
            {
                if (group.Success && int.TryParse(group.Value, out var year) && year >= 1995 && year <= currentYear)
                    latest = latest is null ? year : Math.Max(latest.Value, year);
            }
        }

        return latest;
    }

    // <meta name="viewport" ...> – atrybuty w dowolnej kolejności, cudzysłowy pojedyncze lub podwójne.
    [GeneratedRegex("""<meta\b[^>]*\bname\s*=\s*["']?viewport""", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ViewportRegex();

    // "© 2017", "&copy; 2015-2019", "Copyright 2018 – 2021", "(c) 2016"
    [GeneratedRegex("""(?:©|&copy;|&#169;|copyright|\(c\))\s*(?<from>(?:19|20)\d{2})(?:\s*[-–—]\s*(?<to>(?:19|20)\d{2}))?""", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex CopyrightRegex();
}
