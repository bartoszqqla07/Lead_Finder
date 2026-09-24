using System.Net;
using System.Text;
using LeadFinder.Models;

namespace LeadFinder.Services;

/// <summary>
/// Sprawdza, czy strona biznesu działa, i rozpoznaje jej technologię.
/// Nie wykonuje żadnych opóźnień – rate limiting należy do wywołującego.
/// </summary>
public sealed class WebsiteChecker
{
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(8);

    /// <summary>Wystarczy początek strony: ślady CMS-a są w &lt;head&gt; i pierwszych zasobach.</summary>
    private const int MaxHtmlChars = 512 * 1024;

    /// <summary>
    /// SocketsHttpHandler nie przechodzi automatycznie z https na http. Takie przekierowania
    /// (częste na starych stronach) obsługujemy ręcznie, z tym limitem.
    /// </summary>
    private const int MaxManualRedirects = 3;

    /// <summary>
    /// Domeny, które salony często podają w Google zamiast własnej strony. Taki link nie jest stroną
    /// biznesu, tylko profilem na cudzej platformie, więc go nie pobieramy.
    /// </summary>
    private static readonly (string Domain, string Platform)[] ProfilePlatforms =
    [
        ("booksy.com", "Booksy"),
        ("facebook.com", "Facebook"),
        ("fb.com", "Facebook"),
        ("instagram.com", "Instagram"),
        ("linktr.ee", "Linktree"),
        ("tiktok.com", "TikTok"),
        ("youtube.com", "YouTube"),
    ];

    private readonly HttpClient _httpClient;
    private readonly TimeSpan _timeout;

    /// <param name="httpClient">Klient HTTP; najlepiej z <see cref="CreateHttpClient"/>.</param>
    /// <param name="timeout">Limit czasu na całe sprawdzenie jednej strony (domyślnie 8 s).</param>
    public WebsiteChecker(HttpClient httpClient, TimeSpan? timeout = null)
    {
        _httpClient = httpClient;
        _timeout = timeout ?? DefaultTimeout;
    }

    /// <summary>
    /// Tworzy HttpClient skonfigurowany do sprawdzania stron: przekierowania, dekompresja,
    /// nagłówki przeglądarki (część hostingów odrzuca "gołe" klienty HTTP jako boty).
    /// Timeout klienta jest wyłączony, bo pilnuje go CancellationToken w <see cref="CheckAsync"/>.
    /// </summary>
    public static HttpClient CreateHttpClient()
    {
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 10,
            AutomaticDecompression = DecompressionMethods.All,
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
        };

        var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128.0 Safari/537.36");
        client.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml;q=0.9,*/*;q=0.8");
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("pl-PL,pl;q=0.9,en;q=0.8");
        return client;
    }

    /// <summary>
    /// Pobiera stronę główną i ocenia jej stan.
    /// </summary>
    /// <param name="websiteUri">Adres z pola websiteUri w Google.</param>
    /// <param name="cancellationToken">Anulowanie całego programu (Ctrl+C) – nie mylić z timeoutem strony.</param>
    /// <returns>
    /// Wynik z notatką. Metoda nie rzuca wyjątków dla problemów ze stroną (DNS, SSL, timeout):
    /// to normalny wynik "strona nie działa". Rzuca tylko <see cref="OperationCanceledException"/> przy anulowaniu programu.
    /// </returns>
    public async Task<WebsiteCheckResult> CheckAsync(string websiteUri, CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(websiteUri.Trim(), UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return WebsiteCheckResult.Unreachable($"nieprawidłowy adres: {websiteUri}");
        }

        var platform = DetectProfilePlatform(uri);
        if (platform is not null)
            return WebsiteCheckResult.ProfileOnly(platform);

        // Osobny token z timeoutem strony, powiązany z tokenem programu (Ctrl+C przerywa też bieżące sprawdzenie).
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(_timeout);

        try
        {
            return await FetchAndAnalyzeAsync(uri, timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return WebsiteCheckResult.Unreachable($"brak odpowiedzi w {_timeout.TotalSeconds:0} s (timeout)");
        }
        catch (HttpRequestException ex)
        {
            return WebsiteCheckResult.Unreachable(DescribeConnectionError(ex));
        }
    }

    private async Task<WebsiteCheckResult> FetchAndAnalyzeAsync(Uri uri, CancellationToken cancellationToken)
    {
        var currentUri = uri;
        for (var redirects = 0; ; redirects++)
        {
            using var response = await _httpClient.GetAsync(currentUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            // Przekierowanie, którego handler nie wykonał sam (https → http).
            if (IsRedirect(response.StatusCode) && response.Headers.Location is { } location && redirects < MaxManualRedirects)
            {
                currentUri = location.IsAbsoluteUri ? location : new Uri(currentUri, location);
                continue;
            }

            if (!response.IsSuccessStatusCode)
                return WebsiteCheckResult.Unreachable(DescribeHttpError(response));

            var html = await ReadHtmlPrefixAsync(response.Content, cancellationToken);
            var technology = TechnologyDetector.Detect(html, response.Headers);
            var signals = PageSignals.Detect(html);
            var finalUri = response.RequestMessage?.RequestUri ?? currentUri;

            return new WebsiteCheckResult(
                Reachable: true,
                IsWordPress: technology.IsWordPress,
                Note: BuildSuccessNote(response.StatusCode, technology, signals, uri, finalUri),
                Technology: technology.Name,
                IsMobileFriendly: signals.IsMobileFriendly,
                CopyrightYear: signals.CopyrightYear,
                UsesHttps: finalUri.Scheme == Uri.UriSchemeHttps);
        }
    }

    /// <summary>Stopka starsza niż tyle lat to sygnał, że nikt nie zajmuje się stroną.</summary>
    public const int OutdatedCopyrightYears = 3;

    private static string BuildSuccessNote(
        HttpStatusCode status, TechnologyInfo technology, PageSignals signals, Uri requested, Uri final)
    {
        var parts = new List<string> { $"HTTP {(int)status}" };

        parts.Add(technology.Name switch
        {
            null => "nie rozpoznano CMS",
            var name when technology.Evidence.Count > 0 => $"{name} ({string.Join(", ", technology.Evidence)})",
            var name => name,
        });

        // Przekierowanie na inną domenę bywa sygnałem: wygasła domena → parking, albo przeniesienie na Booksy.
        if (!NormalizeHost(requested.Host).Equals(NormalizeHost(final.Host), StringComparison.OrdinalIgnoreCase))
            parts.Add($"przekierowuje na {final.Host}");

        if (final.Scheme == Uri.UriSchemeHttp)
            parts.Add("brak HTTPS");

        if (!signals.IsMobileFriendly)
            parts.Add("brak wersji na telefon");

        if (signals.CopyrightYear is { } year && year <= DateTime.Now.Year - OutdatedCopyrightYears)
            parts.Add($"stopka © {year}");

        return string.Join(" · ", parts);
    }

    private static string DescribeHttpError(HttpResponseMessage response)
    {
        var code = (int)response.StatusCode;
        var behindCloudflare = response.Headers.Server.Any(s =>
            s.Product?.Name.Equals("cloudflare", StringComparison.OrdinalIgnoreCase) == true);

        // 403/429 często oznacza ochronę przed botami, a nie martwą stronę: sygnał do ręcznej weryfikacji.
        return code switch
        {
            403 or 429 when behindCloudflare => $"HTTP {code} (Cloudflare blokuje boty – sprawdź ręcznie w przeglądarce)",
            401 or 403 or 429 => $"HTTP {code} (może blokować boty – sprawdź ręcznie w przeglądarce)",
            404 => "HTTP 404 (strona nie istnieje)",
            >= 500 => $"HTTP {code} (błąd serwera)",
            _ => $"HTTP {code}",
        };
    }

    private static string DescribeConnectionError(HttpRequestException ex) => ex.HttpRequestError switch
    {
        HttpRequestError.NameResolutionError => "domena nie istnieje lub nie ma rekordów DNS",
        HttpRequestError.ConnectionError => "serwer odrzuca połączenie",
        HttpRequestError.SecureConnectionError => "błąd certyfikatu SSL/TLS (przeglądarka pokaże ostrzeżenie)",
        HttpRequestError.InvalidResponse or HttpRequestError.ResponseEnded => "serwer zwraca niepoprawną odpowiedź",
        _ => $"błąd połączenia: {ex.Message}",
    };

    private static async Task<string> ReadHtmlPrefixAsync(HttpContent content, CancellationToken cancellationToken)
    {
        await using var stream = await content.ReadAsStreamAsync(cancellationToken);
        // Szukane znaczniki są w ASCII, więc ewentualnie błędne dekodowanie strony w innym kodowaniu nie szkodzi.
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

        var buffer = new char[MaxHtmlChars];
        var total = 0;
        int read;
        while (total < buffer.Length && (read = await reader.ReadAsync(buffer.AsMemory(total), cancellationToken)) > 0)
            total += read;

        return new string(buffer, 0, total);
    }

    private static string? DetectProfilePlatform(Uri uri)
    {
        var host = uri.Host.ToLowerInvariant();
        foreach (var (domain, platform) in ProfilePlatforms)
        {
            if (host == domain || host.EndsWith("." + domain, StringComparison.Ordinal))
                return platform;
        }

        return null;
    }

    private static bool IsRedirect(HttpStatusCode status) => (int)status is >= 300 and < 400;

    private static string NormalizeHost(string host) =>
        host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? host[4..] : host;
}
