namespace LeadFinder.Models;

/// <summary>Wynik sprawdzenia strony WWW biznesu.</summary>
/// <param name="Reachable">Czy serwer odpowiedział kodem 2xx.</param>
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
public sealed record WebsiteCheckResult(
    bool Reachable,
    bool IsWordPress,
    string Note,
    string? Technology = null,
    string? ProfilePlatform = null,
    bool? IsMobileFriendly = null,
    int? CopyrightYear = null,
    bool? UsesHttps = null)
{
    public static WebsiteCheckResult Unreachable(string note) => new(false, false, note);

    public static WebsiteCheckResult ProfileOnly(string platform) =>
        new(true, false, $"tylko profil {platform}, brak własnej strony", ProfilePlatform: platform);
}
