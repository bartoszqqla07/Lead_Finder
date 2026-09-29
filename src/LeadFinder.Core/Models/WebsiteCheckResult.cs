using LeadFinder.Services;

namespace LeadFinder.Models;

/// <summary>Wynik sprawdzenia strony WWW biznesu.</summary>
/// <param name="Reachable">
/// Czy strona działa: odpowiedziała treścią (także mimo złego certyfikatu SSL) albo serwer tylko blokuje
/// automaty (401/403/429). False = realnie niedostępna po kilku próbach.
/// </param>
/// <param name="IsWordPress">Czy w HTML znaleziono ślady WordPressa.</param>
/// <param name="Note">Krótka notatka dla człowieka, np. "HTTP 200 · WordPress 5.8 (wp-content, wp-json)".</param>
/// <param name="Technology">Rozpoznany CMS/kreator stron, jeśli udało się go ustalić.</param>
/// <param name="ProfilePlatform">
/// Wypełnione, gdy "strona" w Google to w rzeczywistości profil na platformie (Booksy, Facebook, Instagram…),
/// czyli biznes nie ma własnej strony. Takiego adresu w ogóle nie pobieramy.
/// </param>
/// <param name="IsMobileFriendly">
/// Czy strona deklaruje układ dla telefonów (meta viewport); null, gdy strony nie udało się pobrać.
/// </param>
/// <param name="CopyrightYear">Najnowszy rok ze stopki typu "© 2017"; null, gdy go nie znaleziono.</param>
/// <param name="UsesHttps">Czy strona (po przekierowaniach) działa przez HTTPS; null, gdy nie wiadomo.</param>
/// <param name="ModernMarkers">Ślady nowoczesnej strony (Elementor, WebP, lazy loading…); null, gdy nie sprawdzano.</param>
/// <param name="OutdatedMarkers">Ślady przestarzałej strony (HTML4, tabelki, Flash…); null, gdy nie sprawdzano.</param>
/// <param name="Emails">E-maile znalezione na stronie firmy (główna + "Kontakt"); null, gdy nie sprawdzano.</param>
/// <param name="InstagramUrl">Profil na Instagramie – ze strony firmy, profilu Booksy albo samego linku z Google.</param>
/// <param name="FacebookUrl">Strona na Facebooku – z tych samych źródeł.</param>
public sealed record WebsiteCheckResult(
    bool Reachable,
    bool IsWordPress,
    string Note,
    string? Technology = null,
    string? ProfilePlatform = null,
    bool? IsMobileFriendly = null,
    int? CopyrightYear = null,
    bool? UsesHttps = null,
    IReadOnlyList<string>? ModernMarkers = null,
    IReadOnlyList<string>? OutdatedMarkers = null,
    IReadOnlyList<string>? Emails = null,
    string? InstagramUrl = null,
    string? FacebookUrl = null)
{
    /// <summary>
    /// Strona działa i wygląda na współczesną, zadbaną – WordPress czy nie, trudno sprzedać nową.
    /// Dla leadów sprawdzonych przed dodaniem tych sygnałów (markery = null) zawsze false.
    /// </summary>
    public bool LooksModern =>
        Reachable
        && ProfilePlatform is null
        && ModernMarkers is not null
        && PageSignals.LooksModern(IsMobileFriendly, CopyrightYear, ModernMarkers.Count, OutdatedMarkers?.Count ?? 0);

    public static WebsiteCheckResult Unreachable(string note) => new(false, false, note);

    public static WebsiteCheckResult ProfileOnly(string platform, ContactInfo? contacts = null) =>
        new(true, false, $"tylko profil {platform}, brak własnej strony", ProfilePlatform: platform,
            Emails: contacts?.Emails, InstagramUrl: contacts?.InstagramUrl, FacebookUrl: contacts?.FacebookUrl);
}
