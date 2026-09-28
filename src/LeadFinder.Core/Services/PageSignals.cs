using System.Text.RegularExpressions;

namespace LeadFinder.Services;

/// <summary>Sygnały nowoczesności i przestarzałości strony – wyciągane z HTML strony głównej.</summary>
/// <param name="IsMobileFriendly">Czy strona ma meta viewport (bez niego telefon pokazuje pomniejszoną wersję desktopową).</param>
/// <param name="CopyrightYear">Najnowszy rok ze stopki ("© 2015–2019" → 2019); null, gdy nie znaleziono.</param>
/// <param name="ModernMarkers">
/// Ślady współczesnej, dopracowanej strony: nowoczesny kreator/motyw, obrazy WebP, lazy loading, dane strukturalne…
/// Sam WordPress niczego nie przesądza – stoją na nim zarówno strony z 2012 roku, jak i świetne projekty.
/// </param>
/// <param name="OutdatedMarkers">Ślady strony sprzed lat: HTML4/XHTML, układ na tabelkach, Flash, stare motywy…</param>
public sealed partial record PageSignals(
    bool IsMobileFriendly,
    int? CopyrightYear,
    IReadOnlyList<string> ModernMarkers,
    IReadOnlyList<string> OutdatedMarkers)
{
    /// <summary>Analizuje HTML strony głównej.</summary>
    public static PageSignals Detect(string html) =>
        new(ViewportRegex().IsMatch(html), FindCopyrightYear(html), FindMarkers(html, ModernRules), FindMarkers(html, OutdatedRules));

    /// <summary>(opis dla człowieka, wzorzec w HTML) – kolejność = kolejność w notatce.</summary>
    private static readonly (string Label, Regex Pattern)[] ModernRules =
    [
        ("Elementor", Rx("""elementor""")),
        ("Divi", Rx("""et_pb_|/themes/divi""")),
        ("Bricks", Rx("""brxe-|/themes/bricks""")),
        ("Oxygen/Breakdance", Rx("""/plugins/oxygen|breakdance""")),
        ("nowoczesny motyw WordPressa", Rx("""/themes/(astra|kadence|generatepress|blocksy|hello-elementor|neve|twentytwenty(two|three|four|five))""")),
        ("bloki Gutenberga", Rx("""is-layout-(flex|constrained|grid)""")),
        ("Webflow", Rx("""webflow""")),
        ("Framer", Rx("""framerusercontent|framer\.com""")),
        ("Next.js/Nuxt/Astro", Rx("""/_next/static|/_nuxt/|astro-island|data-astro""")),
        ("Squarespace", Rx("""static1\.squarespace\.com""")),
        ("obrazy WebP/AVIF", Rx("""\.webp\b|\.avif\b|image/webp""")),
        ("lazy loading", Rx("""loading\s*=\s*["']lazy""")),
        ("responsywne obrazy", Rx("""\bsrcset\s*=""")),
        ("dane strukturalne", Rx("""application/ld\+json""")),
        ("widget rezerwacji", Rx("""booksy\.com/widget|widget\.booksy|versum\.(com|pl)|calendly\.com|moment\.pl/widget""")),
    ];

    private static readonly (string Label, Regex Pattern)[] OutdatedRules =
    [
        ("stary standard HTML (XHTML/HTML4)", Rx("""<!DOCTYPE\s+html\s+PUBLIC\s+"-//W3C//DTD""")),
        ("układ strony na tabelkach", Rx("""<table[^>]*\b(cellpadding|cellspacing|width\s*=\s*["']?\d)""")),
        ("przestarzałe znaczniki HTML", Rx("""<font\b|<center>|<marquee""")),
        ("Flash", Rx("""\.swf\b|shockwave-flash""")),
        ("ramki (frameset)", Rx("""<frameset|<frame\s""")),
        ("stary motyw WordPressa", Rx("""/themes/twenty(ten|eleven|twelve|thirteen|fourteen|fifteen)/""")),
        ("stary jQuery 1.x", Rx("""jquery[.-]1\.\d+(\.\d+)?(\.min)?\.js|jquery(\.min)?\.js\?ver=1\.""")),
        ("stara Joomla (MooTools)", Rx("""mootools""")),
    ];

    /// <summary>
    /// Czy strona wygląda na współczesną i zadbaną: działa na telefonie, ma co najmniej dwa nowoczesne elementy
    /// i żadnego przestarzałego. Takiej firmie trudno sprzedać nową stronę.
    /// </summary>
    /// <remarks>
    /// Stara stopka ("© 2023") przekreśla to tylko przy skromnych sygnałach – na dopracowanych stronach nikt jej
    /// nie aktualizuje. Brak HTTPS też nie: to drobna poprawka, nie powód do nowej strony (osobno daje punkty).
    /// </remarks>
    public static bool LooksModern(bool? isMobileFriendly, int? copyrightYear, int modernCount, int outdatedCount) =>
        isMobileFriendly == true
        && modernCount >= 2
        && outdatedCount == 0
        && (modernCount >= 3 || copyrightYear is null || copyrightYear > DateTime.Now.Year - WebsiteChecker.OutdatedCopyrightYears);

    private static IReadOnlyList<string> FindMarkers(string html, (string Label, Regex Pattern)[] rules) =>
        rules.Where(rule => rule.Pattern.IsMatch(html)).Select(rule => rule.Label).ToList();

    private static Regex Rx(string pattern) =>
        new(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled, TimeSpan.FromSeconds(1));

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
