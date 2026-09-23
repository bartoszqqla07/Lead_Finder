using System.Net.Http.Headers;
using System.Text.RegularExpressions;

namespace LeadFinder.Services;

/// <summary>Rozpoznana technologia strony.</summary>
/// <param name="Name">Nazwa CMS/kreatora albo null, jeśli nie rozpoznano.</param>
/// <param name="IsWordPress">Czy to WordPress.</param>
/// <param name="Evidence">Znalezione ślady, np. ["wp-content", "wp-json"], do notatki w CSV.</param>
public sealed record TechnologyInfo(string? Name, bool IsWordPress, IReadOnlyList<string> Evidence);

/// <summary>
/// Heurystyczne rozpoznawanie CMS-a na podstawie HTML strony głównej i nagłówków HTTP.
/// </summary>
public static partial class TechnologyDetector
{
    /// <summary>
    /// Ślady WordPressa w HTML:
    ///  - "wp-content"  – katalog motywów, wtyczek i uploadów; pojawia się w niemal każdym &lt;link&gt;/&lt;img&gt;,
    ///  - "wp-includes" – skrypty i style rdzenia (np. jQuery dostarczane z WP),
    ///  - "wp-json"     – REST API, linkowane w &lt;head&gt; jako &lt;link rel="https://api.w.org/"&gt;.
    /// Wystarczy jeden ślad: fałszywe trafienia są rzadkie. Strony, które ukrywają WP wtyczkami
    /// typu "hide my WP", i tak zostaną rozpoznane jako "nie rozpoznano CMS".
    /// </summary>
    private static readonly string[] WordPressMarkers = ["wp-content", "wp-includes", "wp-json"];

    /// <summary>Inne popularne kreatory – tylko do kolumny "Technologia", nie wpływają na status.</summary>
    private static readonly (string Name, string Marker)[] OtherPlatforms =
    [
        ("Wix", "static.wixstatic.com"),
        ("Squarespace", "static1.squarespace.com"),
        ("Shopify", "cdn.shopify.com"),
        ("Webflow", "data-wf-site"),
        ("Joomla", "/media/jui/"),
    ];

    /// <summary>
    /// Rozpoznaje technologię strony.
    /// </summary>
    /// <param name="html">Początek HTML strony głównej.</param>
    /// <param name="headers">Nagłówki odpowiedzi (WordPress wysyła w nich np. Link do api.w.org).</param>
    public static TechnologyInfo Detect(string html, HttpResponseHeaders headers)
    {
        var wordPressEvidence = WordPressMarkers
            .Where(marker => html.Contains(marker, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (headers.TryGetValues("Link", out var links) && links.Any(l => l.Contains("api.w.org", StringComparison.OrdinalIgnoreCase)))
            wordPressEvidence.Add("nagłówek Link");

        var generator = GeneratorMetaRegex().Match(html) is { Success: true } match
            ? match.Groups["content"].Value.Trim()
            : null;

        if (wordPressEvidence.Count > 0 || generator?.StartsWith("WordPress", StringComparison.OrdinalIgnoreCase) == true)
        {
            // Wersja z <meta name="generator" content="WordPress 6.4.2"> bywa usuwana, ale gdy jest,
            // stara wersja to dobry argument w rozmowie ("strona nie była aktualizowana od lat").
            var name = generator?.StartsWith("WordPress", StringComparison.OrdinalIgnoreCase) == true ? generator : "WordPress";
            return new TechnologyInfo(name, IsWordPress: true, wordPressEvidence);
        }

        foreach (var (platformName, marker) in OtherPlatforms)
        {
            if (html.Contains(marker, StringComparison.OrdinalIgnoreCase))
                return new TechnologyInfo(platformName, IsWordPress: false, [marker]);
        }

        if (headers.Contains("x-wix-request-id"))
            return new TechnologyInfo("Wix", IsWordPress: false, ["nagłówek x-wix-request-id"]);

        // Ostatnia deska ratunku: cokolwiek strona deklaruje w meta generator (np. "Joomla! 3", "WebWave").
        return new TechnologyInfo(string.IsNullOrEmpty(generator) ? null : generator, IsWordPress: false, []);
    }

    // <meta name="generator" content="..."> z atrybutami w dowolnej kolejności:
    // lookahead sprawdza name="generator" w obrębie tagu, zanim złapiemy content.
    [GeneratedRegex("""<meta\b(?=[^>]*\bname\s*=\s*["']generator["'])[^>]*\bcontent\s*=\s*["'](?<content>[^"']{1,80})["']""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex GeneratorMetaRegex();
}
