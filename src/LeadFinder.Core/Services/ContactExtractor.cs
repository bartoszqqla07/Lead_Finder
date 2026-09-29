using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace LeadFinder.Services;

/// <summary>Kontakty znalezione na stronie firmy (albo na jej profilu Booksy).</summary>
/// <param name="Emails">Adresy e-mail – najpierw te w domenie firmy.</param>
/// <param name="InstagramUrl">Profil firmy na Instagramie, jeśli strona do niego linkuje.</param>
/// <param name="FacebookUrl">Strona firmy na Facebooku, jeśli strona do niej linkuje.</param>
public sealed record ContactInfo(IReadOnlyList<string> Emails, string? InstagramUrl, string? FacebookUrl)
{
    public static readonly ContactInfo Empty = new([], null, null);

    /// <summary>Mamy wszystko, czego szukamy – dalszych podstron nie trzeba pobierać.</summary>
    public bool IsComplete => Emails.Count > 0 && InstagramUrl is not null && FacebookUrl is not null;

    /// <summary>Łączy kontakty ze strony głównej i podstrony "Kontakt" (bez duplikatów).</summary>
    public ContactInfo Merge(ContactInfo other) => new(
        Emails.Concat(other.Emails).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
        InstagramUrl ?? other.InstagramUrl,
        FacebookUrl ?? other.FacebookUrl);
}

/// <summary>
/// Wyciąga z HTML adresy e-mail i linki do profili społecznościowych firmy. Google Places nie podaje e-maili,
/// więc to jedyne automatyczne źródło – działa dla firm, które mają własną stronę.
/// </summary>
public static partial class ContactExtractor
{
    private const int MaxEmails = 3;

    /// <summary>Konta samych platform (stopki Booksy, wtyczki "udostępnij") – to nie są profile salonu.</summary>
    private static readonly string[] PlatformAccounts =
        ["booksypolska", "booksy_poland", "booksy", "booksyapp", "wix", "wordpress", "facebook", "instagram"];

    /// <summary>Domeny, których adresy nie należą do salonu (platformy, hostingi, przykłady, trackery).</summary>
    private static readonly string[] IgnoredEmailDomains =
    [
        "booksy.com", "example.com", "example.pl", "domain.com", "domena.pl", "email.com", "sentry.io", "wixpress.com",
        "sentry-next.wixpress.com", "wordpress.org", "wordpress.com", "w3.org", "schema.org", "godaddy.com",
        "cloudflare.com", "google.com", "facebook.com", "instagram.com", "home.pl", "nazwa.pl", "cyberfolks.pl",
        "sentry.wixpress.com", "yourdomain.com", "twojadomena.pl",
    ];

    private static readonly string[] ImageExtensions = [".png", ".jpg", ".jpeg", ".gif", ".webp", ".svg", ".avif"];

    /// <summary>Kontakty z HTML strony. <paramref name="pageUri"/> służy do rozpoznania "własnej" domeny.</summary>
    public static ContactInfo Extract(string html, Uri pageUri)
    {
        var candidates = new List<string>();

        foreach (Match m in MailtoRegex().Matches(html))
            candidates.Add(WebUtility.UrlDecode(m.Groups["email"].Value));
        foreach (Match m in CloudflareEmailRegex().Matches(html))
            if (DecodeCloudflareEmail(m.Groups["hex"].Value) is { } decoded)
                candidates.Add(decoded);
        foreach (Match m in ObfuscatedEmailRegex().Matches(html))
            candidates.Add($"{m.Groups["user"].Value}@{m.Groups["domain"].Value}");

        // Zwykły tekst dopiero na końcu – najwięcej w nim śmieci (nazwy plików, przykłady w skryptach).
        var text = WebUtility.HtmlDecode(html);
        foreach (Match m in PlainEmailRegex().Matches(text))
            candidates.Add(m.Value);

        var ownHost = SiteHost(pageUri.Host);
        var emails = candidates
            .Select(e => e.Trim().TrimEnd('.').ToLowerInvariant())
            .Where(IsPlausibleEmail)
            .Distinct()
            // Najpierw adresy "do klientów" (kontakt@, biuro@…), potem w domenie salonu, na końcu księgowość/rekrutacja.
            .OrderBy(e => MailboxRank(e[..e.IndexOf('@')]))
            .ThenBy(e => IsOwnDomain(e[(e.IndexOf('@') + 1)..], ownHost) ? 0 : 1)
            .Take(MaxEmails)
            .ToList();

        return new ContactInfo(
            emails,
            FindProfile(html, InstagramRegex()) ?? FindInstagramHandleInText(text),
            FindProfile(html, FacebookRegex()) ?? FindFacebookProfileId(html));
    }

    /// <summary>Strona bez własnej nazwy: facebook.com/profile.php?id=123… (także w JSON: \/profile.php?id=…).</summary>
    private static string? FindFacebookProfileId(string html) =>
        FacebookProfileIdRegex().Match(html) is { Success: true } m
            ? $"https://www.facebook.com/profile.php?id={m.Groups["id"].Value}"
            : null;

    /// <summary>
    /// Podstrony, na których zwykle są kontakty – najpierw "Kontakt", potem "O nas" – w tej samej domenie.
    /// </summary>
    public static IEnumerable<Uri> FindContactPages(string html, Uri pageUri)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var links = ContactLinkRegex().Matches(html).Concat(AboutLinkRegex().Matches(html));
        foreach (var m in links)
        {
            if (!Uri.TryCreate(pageUri, WebUtility.HtmlDecode(m.Groups["href"].Value), out var link))
                continue;
            if (link.Scheme is not ("http" or "https"))
                continue;
            if (SiteHost(link.Host) != SiteHost(pageUri.Host))
                continue;
            if (link.AbsolutePath.TrimEnd('/') == pageUri.AbsolutePath.TrimEnd('/'))
                continue;
            if (seen.Add(link.GetLeftPart(UriPartial.Path)))
                yield return link;
        }
    }

    /// <summary>"Instagram: @salon_xyz" – profil podany tekstem, bez linku.</summary>
    private static string? FindInstagramHandleInText(string text)
    {
        foreach (Match m in InstagramHandleTextRegex().Matches(text))
        {
            var handle = m.Groups["handle"].Value.TrimEnd('.');
            var at = m.Groups["handle"].Index - 1;
            // "kontakt@salon.pl" (znak przed @ to część adresu e-mail) albo reguła CSS po słowie "instagram".
            if (at > 0 && char.IsLetterOrDigit(text[at - 1]))
                continue;
            if (NotInstagramHandles.Contains(handle) || DomainLikeRegex().IsMatch(handle))
                continue;
            if (PlatformAccounts.Contains(handle, StringComparer.OrdinalIgnoreCase))
                continue;
            return $"https://www.instagram.com/{handle}/";
        }

        return null;
    }

    private static readonly string[] CustomerMailboxes =
        ["kontakt", "biuro", "recepcja", "rezerwacje", "rezerwacja", "salon", "studio", "info", "hello", "hej", "office", "gabinet"];

    private static readonly string[] BackOfficeMailboxes =
        ["rozliczenia", "faktury", "ksiegowosc", "sklep", "szkolenia", "akademia", "rekrutacja", "praca", "kariera", "hr",
         "marketing", "reklama", "newsletter", "admin", "webmaster", "rodo", "iod"];

    /// <summary>0 = skrzynka dla klientów, 1 = inna, 2 = zaplecze (faktury, rekrutacja…).</summary>
    private static int MailboxRank(string localPart) =>
        CustomerMailboxes.Any(m => localPart.StartsWith(m, StringComparison.Ordinal)) ? 0
        : BackOfficeMailboxes.Any(m => localPart.StartsWith(m, StringComparison.Ordinal)) ? 2
        : 1;

    /// <summary>Słowa po "@" w CSS/JS, które nie są nazwami kont.</summary>
    private static readonly HashSet<string> NotInstagramHandles = new(StringComparer.OrdinalIgnoreCase)
    {
        "media", "import", "keyframes", "font-face", "supports", "charset", "context", "type", "graph", "id", "vocab",
        "container", "layer", "page", "property",
    };

    private static bool IsPlausibleEmail(string email)
    {
        var at = email.IndexOf('@');
        if (at <= 0 || email.Length > 80)
            return false;

        var domain = email[(at + 1)..];
        if (!domain.Contains('.') || ImageExtensions.Any(ext => email.EndsWith(ext, StringComparison.Ordinal)))
            return false;
        if (IgnoredEmailDomains.Any(d => domain == d || domain.EndsWith("." + d, StringComparison.Ordinal)))
            return false;

        // Hash-e z bundli JS ("a3f9c…@2x"), adresy techniczne i szablonowe.
        // Jednoliterowa "nazwa" to prawie zawsze urywek tekstu ("n@keune.akademia"), nie skrzynka salonu.
        var user = email[..at];
        return user.Length >= 2
            && !HexHashRegex().IsMatch(user)
            && user is not ("user" or "name" or "email" or "your-email" or "twojemail" or "imie.nazwisko" or "noreply" or "no-reply");
    }

    private static string? FindProfile(string html, Regex regex)
    {
        foreach (Match m in regex.Matches(html))
        {
            var handle = m.Groups["handle"].Value.TrimEnd('/');
            if (handle.Length < 2 || PlatformAccounts.Contains(handle, StringComparer.OrdinalIgnoreCase))
                continue;
            // profile.php to nie nazwa strony – obsługuje go FindFacebookProfileId (z numerem id).
            if (handle is "sharer" or "sharer.php" or "share.php" or "plugins" or "tr" or "dialog" or "p" or "reel" or "explore" or "watch" or "groups" or "profile.php")
                continue;
            return $"https://www.{m.Groups["site"].Value.ToLowerInvariant()}/{handle}/";
        }

        return null;
    }

    /// <summary>Cloudflare "Email Address Obfuscation": pierwszy bajt to klucz XOR dla pozostałych.</summary>
    private static string? DecodeCloudflareEmail(string hex)
    {
        if (hex.Length < 4 || hex.Length % 2 != 0)
            return null;
        try
        {
            var key = Convert.ToByte(hex[..2], 16);
            var bytes = new byte[hex.Length / 2 - 1];
            for (var i = 0; i < bytes.Length; i++)
                bytes[i] = (byte)(byte.Parse(hex.AsSpan(2 + i * 2, 2), NumberStyles.HexNumber) ^ key);
            var decoded = Encoding.UTF8.GetString(bytes);
            return decoded.Contains('@') ? decoded : null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static string SiteHost(string host) =>
        host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? host[4..].ToLowerInvariant() : host.ToLowerInvariant();

    /// <summary>kontakt@salon.pl na www.salon.pl albo biuro@salon.pl na rezerwacje.salon.pl.</summary>
    private static bool IsOwnDomain(string emailDomain, string siteHost) =>
        emailDomain == siteHost
        || siteHost.EndsWith("." + emailDomain, StringComparison.Ordinal)
        || emailDomain.EndsWith("." + siteHost, StringComparison.Ordinal);

    [GeneratedRegex("""mailto:(?<email>[^"'?>\s]+)""", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex MailtoRegex();

    [GeneratedRegex("""data-cfemail\s*=\s*["'](?<hex>[0-9a-fA-F]+)["']|/cdn-cgi/l/email-protection#(?<hex>[0-9a-fA-F]+)""", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex CloudflareEmailRegex();

    // "kontakt [at] salon.pl", "kontakt(at)salon(dot)pl" – tylko z jawnymi nawiasami, żeby nie łapać zwykłego tekstu.
    [GeneratedRegex("""(?<user>[a-z0-9._%+-]+)\s*[\[\(\{]\s*(?:at|małpa|malpa)\s*[\]\)\}]\s*(?<domain>[a-z0-9-]+(?:\.[a-z0-9-]+)*\.[a-z]{2,})""", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ObfuscatedEmailRegex();

    [GeneratedRegex("""\b[a-z0-9][a-z0-9._%+-]{0,63}@[a-z0-9-]+(?:\.[a-z0-9-]+)*\.[a-z]{2,24}\b""", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex PlainEmailRegex();

    [GeneratedRegex("""^[0-9a-f]{16,}$""", RegexOptions.CultureInvariant)]
    private static partial Regex HexHashRegex();

    // Także w JSON-ie osadzonym w stronie (Wix, Next.js), gdzie ukośniki są poprzedzone "\": https:\/\/instagram.com\/salon
    [GeneratedRegex("""https?:\\?/\\?/(?:www\.|m\.)?(?<site>instagram\.com)\\?/(?<handle>[A-Za-z0-9._]{2,30})(?:\\?/)?(?=["'?#\s<\\])""", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex InstagramRegex();

    [GeneratedRegex("""https?:\\?/\\?/(?:www\.|m\.|pl-pl\.)?(?<site>facebook\.com)\\?/(?<handle>[A-Za-z0-9.\-]{2,60})(?:\\?/)?(?=["'?#\s<\\])""", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex FacebookRegex();

    [GeneratedRegex("""facebook\.com\\?/profile\.php\?id(?:=|\\u003d|%3D)(?<id>\d{5,20})""", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex FacebookProfileIdRegex();

    [GeneratedRegex("""<a\b[^>]*\bhref\s*=\s*["'](?<href>[^"'#]*(?:kontakt|contact)[^"'#]*)["']""", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ContactLinkRegex();

    [GeneratedRegex("""<a\b[^>]*\bhref\s*=\s*["'](?<href>[^"'#]*(?:o-nas|onas|about|o-salonie|o-mnie)[^"'#]*)["']""", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex AboutLinkRegex();

    // "Instagram: @salon_xyz", "IG @salon.xyz", "instagram – @salon" – profil podany tekstem, bez linku.
    [GeneratedRegex("""(?:instagram|\binsta\b|\bIG\b)[^@\n<]{0,25}@(?<handle>[A-Za-z0-9._]{3,30})""", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex InstagramHandleTextRegex();

    [GeneratedRegex("""\.(pl|com|eu|net|org|info|biz)$""", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DomainLikeRegex();
}
