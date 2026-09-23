namespace LeadFinder.Models;

/// <summary>Kategoria biznesu wczytana z Config/categories.json.</summary>
/// <param name="Id">Stały identyfikator, np. "barber-shop".</param>
/// <param name="Query">Fraza do Google, np. "barber shop" (zapytanie to "{Query} {miasto}").</param>
/// <param name="EnglishName">Nazwa angielska – pod przyszłe prompty AI / SEO.</param>
/// <param name="Aliases">Dodatkowe nazwy akceptowane w --categories.</param>
/// <param name="Tone">Klucz tonu wiadomości w <see cref="Services.MessageDrafter"/>.</param>
public sealed record Category(
    string Id,
    string Query,
    string EnglishName,
    IReadOnlyList<string> Aliases,
    string Tone);
