using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using LeadFinder.Models;

namespace LeadFinder.Services;

/// <summary>
/// Sprawdza, czy strona biznesu działa, i rozpoznaje jej technologię.
/// Nie wykonuje opóźnień między stronami – rate limiting należy do wywołującego; opóźnia tylko ponowne próby.
/// </summary>
/// <remarks>
/// "Nie działa" to najgorętszy lead, więc fałszywy alarm jest drogi (wiadomość „strona się nie otwiera”
/// do salonu, któremu strona działa). Dlatego: chwilowe błędy sieci są ponawiane, blokada botów (403)
/// i zły certyfikat SSL nie oznaczają martwej strony, a przekierowanie na Facebooka to "tylko profil".
/// </remarks>
public sealed class WebsiteChecker
{
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(8);

    /// <summary>Przerwy przed kolejnymi próbami przy chwilowych błędach (DNS, połączenie, timeout, 5xx).</summary>
    private static readonly TimeSpan[] RetryDelays = [TimeSpan.FromSeconds(1.5), TimeSpan.FromSeconds(4)];

    /// <summary>Wystarczy początek strony: ślady CMS-a są w &lt;head&gt; i pierwszych zasobach.</summary>
    private const int MaxHtmlChars = 512 * 1024;

    /// <summary>
    /// SocketsHttpHandler nie przechodzi automatycznie z https na http. Takie przekierowania
    /// (częste na starych stronach) obsługujemy ręcznie, z tym limitem.
    /// </summary>
    private const int MaxManualRedirects = 3;

    /// <summary>
    /// Domeny, które salony często podają w Google zamiast własnej strony: platformy rezerwacji, media
    /// społecznościowe i katalogi firm. Taki link (albo domena, która na niego przekierowuje) nie jest
    /// stroną biznesu, tylko profilem na cudzej platformie.
    /// </summary>
    private static readonly (string Domain, string Platform)[] ProfilePlatforms =
    [
        ("booksy.com", "Booksy"),
        ("facebook.com", "Facebook"),
        ("facebook.pl", "Facebook"),
        ("fb.com", "Facebook"),
        ("fb.me", "Facebook"),
        ("instagram.com", "Instagram"),
        ("instagr.am", "Instagram"),
        ("linktr.ee", "Linktree"),
        ("tiktok.com", "TikTok"),
        ("youtube.com", "YouTube"),
        ("fresha.com", "Fresha"),
        ("treatwell.pl", "Treatwell"),
        ("moment.pl", "Moment"),
        ("gowork.pl", "GoWork"),
        ("cylex-polska.pl", "Cylex"),
        ("panoramafirm.pl", "Panorama Firm"),
        ("pkt.pl", "PKT"),
        ("oferteo.pl", "Oferteo"),
        ("bliskausluga.pl", "BliskaUsługa"),
        ("dbajosiebie.com", "DbajOSiebie"),
        ("znanylekarz.pl", "ZnanyLekarz"),
    ];

    private readonly HttpClient _httpClient;
    private readonly HttpClient _lenientHttpClient;
    private readonly TimeSpan _timeout;

    /// <param name="httpClient">Klient HTTP; najlepiej z <see cref="CreateHttpClient"/>.</param>
    /// <param name="timeout">Limit czasu na jedną próbę pobrania strony (domyślnie 8 s).</param>
    /// <param name="lenientHttpClient">
    /// Klient akceptujący nieprawidłowe certyfikaty – tylko po to, żeby sprawdzić, czy strona z błędem SSL
    /// poza tym działa. Domyślnie tworzony przez <see cref="CreateHttpClient"/>.
    /// </param>
    public WebsiteChecker(HttpClient httpClient, TimeSpan? timeout = null, HttpClient? lenientHttpClient = null)
    {
        _httpClient = httpClient;
        _timeout = timeout ?? DefaultTimeout;
        _lenientHttpClient = lenientHttpClient ?? CreateHttpClient(acceptInvalidCertificates: true);
    }

    /// <summary>
    /// Tworzy HttpClient skonfigurowany do sprawdzania stron: przekierowania, dekompresja,
    /// nagłówki przeglądarki (część hostingów odrzuca "gołe" klienty HTTP jako boty).
    /// Timeout klienta jest wyłączony, bo pilnuje go CancellationToken w <see cref="CheckAsync"/>.
    /// </summary>
    /// <param name="acceptInvalidCertificates">
    /// Akceptuj każdy certyfikat – wyłącznie do diagnozy stron z błędem SSL, nigdy do przesyłania danych.
    /// </param>
    public static HttpClient CreateHttpClient(bool acceptInvalidCertificates = false)
    {
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 10,
            AutomaticDecompression = DecompressionMethods.All,
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
        };
        if (acceptInvalidCertificates)
            handler.SslOptions.RemoteCertificateValidationCallback = (_, _, _, _) => true;

        var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128.0 Safari/537.36");
        client.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml;q=0.9,*/*;q=0.8");
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("pl-PL,pl;q=0.9,en;q=0.8");
        return client;
    }

    /// <summary>
    /// Pobiera stronę główną i ocenia jej stan. Chwilowe błędy (DNS, połączenie, timeout, 5xx) ponawia
    /// do trzech prób, zanim uzna stronę za niedziałającą.
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

        if (DetectProfilePlatform(uri) is { } platform)
            return WebsiteCheckResult.ProfileOnly(platform);

        for (var attempt = 0; ; attempt++)
        {
            var (result, transient) = await AttemptAsync(uri, cancellationToken);
            if (!transient || attempt >= RetryDelays.Length)
                return result;

            await Task.Delay(RetryDelays[attempt], cancellationToken);
        }
    }

    /// <summary>Jedna próba; <c>Transient</c> = błąd, który warto powtórzyć.</summary>
    private async Task<(WebsiteCheckResult Result, bool Transient)> AttemptAsync(Uri uri, CancellationToken cancellationToken)
    {
        // Osobny token z timeoutem strony, powiązany z tokenem programu (Ctrl+C przerywa też bieżące sprawdzenie).
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(_timeout);

        try
        {
            return await FetchAndAnalyzeAsync(_httpClient, uri, certificateProblem: null, timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return (WebsiteCheckResult.Unreachable($"brak odpowiedzi w {_timeout.TotalSeconds:0} s (timeout, 3 próby)"), true);
        }
        catch (HttpRequestException ex) when (ex.HttpRequestError == HttpRequestError.SecureConnectionError)
        {
            return (await CheckDespiteCertificateErrorAsync(uri, cancellationToken), false);
        }
        catch (HttpRequestException ex)
        {
            var transient = ex.HttpRequestError is HttpRequestError.NameResolutionError or HttpRequestError.ConnectionError
                or HttpRequestError.InvalidResponse or HttpRequestError.ResponseEnded or HttpRequestError.Unknown;
            return (WebsiteCheckResult.Unreachable(DescribeConnectionError(ex)), transient);
        }
    }

    /// <summary>
    /// Zły certyfikat (wygasły, wystawiony dla hostingu zamiast domeny) to wada strony, ale nie martwa strona:
    /// sprawdzamy ją jeszcze raz bez weryfikacji certyfikatu i zapisujemy problem w notatce.
    /// </summary>
    private async Task<WebsiteCheckResult> CheckDespiteCertificateErrorAsync(Uri uri, CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(_timeout * 2);

        try
        {
            // Błąd może dotyczyć adresu po przekierowaniu (http → https), więc diagnozujemy wersję https.
            var httpsUri = new UriBuilder(uri) { Scheme = Uri.UriSchemeHttps, Port = -1 }.Uri;
            var problem = await DescribeCertificateProblemAsync(httpsUri, timeoutCts.Token);
            var (result, _) = await FetchAndAnalyzeAsync(_lenientHttpClient, uri, problem, timeoutCts.Token);
            return result;
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException && !cancellationToken.IsCancellationRequested)
        {
            return WebsiteCheckResult.Unreachable("błąd certyfikatu SSL i brak odpowiedzi strony");
        }
    }

    private async Task<(WebsiteCheckResult Result, bool Transient)> FetchAndAnalyzeAsync(
        HttpClient client, Uri uri, string? certificateProblem, CancellationToken cancellationToken)
    {
        var currentUri = uri;
        for (var redirects = 0; ; redirects++)
        {
            using var response = await client.GetAsync(currentUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            var finalUri = response.RequestMessage?.RequestUri ?? currentUri;

            // Domena salonu przekierowuje na Facebooka/Booksy – to profil, nie strona (Facebook dodatkowo
            // odpowiada botom kodem 400, co wyglądało jak awaria).
            if (DetectProfilePlatform(finalUri) is { } platform)
                return (WebsiteCheckResult.ProfileOnly(platform), false);

            // Przekierowanie, którego handler nie wykonał sam (https → http).
            if (IsRedirect(response.StatusCode) && response.Headers.Location is { } location && redirects < MaxManualRedirects)
            {
                currentUri = location.IsAbsoluteUri ? location : new Uri(currentUri, location);
                continue;
            }

            var code = (int)response.StatusCode;

            // Serwer odpowiada, tylko nie wpuszcza automatów – strona istnieje, nie jest "martwa".
            if (code is 401 or 403 or 429)
                return (BlockedResult(response, finalUri), false);

            if (!response.IsSuccessStatusCode)
                return (WebsiteCheckResult.Unreachable(DescribeHttpError(response)), code >= 500);

            var html = await ReadHtmlPrefixAsync(response.Content, cancellationToken);
            var technology = TechnologyDetector.Detect(html, response.Headers);
            var signals = PageSignals.Detect(html);

            return (new WebsiteCheckResult(
                Reachable: true,
                IsWordPress: technology.IsWordPress,
                Note: BuildSuccessNote(response.StatusCode, technology, signals, uri, finalUri, certificateProblem),
                Technology: technology.Name,
                IsMobileFriendly: signals.IsMobileFriendly,
                CopyrightYear: signals.CopyrightYear,
                // Zły certyfikat = odwiedzający widzą ostrzeżenie zamiast bezpiecznego HTTPS.
                UsesHttps: certificateProblem is null && finalUri.Scheme == Uri.UriSchemeHttps), false);
        }
    }

    private static WebsiteCheckResult BlockedResult(HttpResponseMessage response, Uri finalUri)
    {
        var code = (int)response.StatusCode;
        var behindCloudflare = response.Headers.Server.Any(s =>
            s.Product?.Name.Equals("cloudflare", StringComparison.OrdinalIgnoreCase) == true);
        var who = behindCloudflare ? "Cloudflare blokuje" : "strona blokuje";
        return new WebsiteCheckResult(
            Reachable: true,
            IsWordPress: false,
            Note: $"HTTP {code} – {who} automatyczne sprawdzanie; strona działa, wygląd oceń w przeglądarce",
            UsesHttps: finalUri.Scheme == Uri.UriSchemeHttps);
    }

    /// <summary>
    /// Nawiązuje samo połączenie TLS i opisuje, co jest nie tak z certyfikatem – tak, jak zobaczy to klient
    /// salonu: "wygasł 26.09.2026" albo "wystawiony dla innej domeny (seohost.pl)".
    /// </summary>
    private static async Task<string> DescribeCertificateProblemAsync(Uri uri, CancellationToken cancellationToken)
    {
        const string generic = "nieprawidłowy certyfikat SSL – przeglądarka pokazuje ostrzeżenie";
        try
        {
            using var tcp = new TcpClient();
            await tcp.ConnectAsync(uri.Host, uri.Port, cancellationToken);

            var errors = SslPolicyErrors.None;
            X509Certificate2? certificate = null;
            var chainStatus = X509ChainStatusFlags.NoError;
            await using var ssl = new SslStream(tcp.GetStream(), leaveInnerStreamOpen: false, (_, cert, chain, policyErrors) =>
            {
                errors = policyErrors;
                certificate = cert is null ? null : new X509Certificate2(cert);
                foreach (var status in chain?.ChainStatus ?? [])
                    chainStatus |= status.Status;
                return true;
            });
            await ssl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions { TargetHost = uri.Host }, cancellationToken);

            using (certificate)
            {
                if (chainStatus.HasFlag(X509ChainStatusFlags.NotTimeValid) && certificate is not null)
                    return $"certyfikat SSL wygasł {certificate.NotAfter:dd.MM.yyyy} – przeglądarka pokazuje ostrzeżenie";
                if (errors.HasFlag(SslPolicyErrors.RemoteCertificateNameMismatch) && certificate is not null)
                    return $"certyfikat SSL wystawiony dla innej domeny ({certificate.GetNameInfo(X509NameType.DnsName, false)}) – przeglądarka pokazuje ostrzeżenie";
                return generic;
            }
        }
        catch (Exception ex) when (ex is SocketException or IOException or System.Security.Authentication.AuthenticationException)
        {
            return generic;
        }
    }

    /// <summary>Stopka starsza niż tyle lat to sygnał, że nikt nie zajmuje się stroną.</summary>
    public const int OutdatedCopyrightYears = 3;

    private static string BuildSuccessNote(
        HttpStatusCode status, TechnologyInfo technology, PageSignals signals, Uri requested, Uri final, string? certificateProblem)
    {
        var parts = new List<string> { $"HTTP {(int)status}" };

        parts.Add(technology.Name switch
        {
            null => "nie rozpoznano CMS",
            var name when technology.Evidence.Count > 0 => $"{name} ({string.Join(", ", technology.Evidence)})",
            var name => name,
        });

        if (certificateProblem is not null)
            parts.Add(certificateProblem);

        // Przekierowanie na inną domenę bywa sygnałem: wygasła domena → parking, albo przeniesienie na inną stronę.
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

    private static string DescribeHttpError(HttpResponseMessage response) => (int)response.StatusCode switch
    {
        404 => "HTTP 404 (strona nie istnieje)",
        410 => "HTTP 410 (strona usunięta)",
        var code and >= 500 => $"HTTP {code} (błąd serwera, 3 próby)",
        var code => $"HTTP {code}",
    };

    private static string DescribeConnectionError(HttpRequestException ex) => ex.HttpRequestError switch
    {
        HttpRequestError.NameResolutionError => "domena nie istnieje lub nie ma rekordów DNS (3 próby)",
        HttpRequestError.ConnectionError => "serwer odrzuca połączenie (3 próby)",
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
