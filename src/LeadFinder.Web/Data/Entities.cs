using LeadFinder.Models;

namespace LeadFinder.Web.Data;

/// <summary>Etap kontaktu z leadem (mini-CRM). Ustawiany ręcznie przez użytkownika.</summary>
public enum OutreachStage
{
    New,
    Contacted,
    Replied,
    Client,
    Rejected,
}

public enum SearchRunState
{
    Running,
    Completed,
    Failed,
    Cancelled,
}

/// <summary>
/// Lead zapisany w bazie. Dane z Google i wynik sprawdzenia strony są odświeżane przy każdym
/// wyszukiwaniu, a pola CRM (<see cref="Stage"/>, <see cref="Notes"/>) należą do użytkownika i nie są nadpisywane.
/// </summary>
public sealed class LeadEntity
{
    public int Id { get; set; }

    /// <summary>place.id z Google – klucz deduplikacji między wyszukiwaniami.</summary>
    public required string PlaceId { get; set; }

    public required string Name { get; set; }
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? WebsiteUri { get; set; }
    public double? Rating { get; set; }
    public int? UserRatingCount { get; set; }

    /// <summary>OPERATIONAL / CLOSED_TEMPORARILY / CLOSED_PERMANENTLY; null dla leadów sprzed dodania pola.</summary>
    public string? BusinessStatus { get; set; }

    public required string City { get; set; }
    public required string CategoryId { get; set; }
    public required string CategoryQuery { get; set; }
    public required string CategoryTone { get; set; }

    public LeadStatus Status { get; set; }
    public bool? WebsiteReachable { get; set; }
    public bool IsWordPress { get; set; }
    public string? CheckNote { get; set; }
    public string? Technology { get; set; }
    public string? ProfilePlatform { get; set; }

    // Sygnały przestarzałej strony (do oceny szansy); null = nie sprawdzano albo lead sprzed dodania pól.
    public bool? IsMobileFriendly { get; set; }
    public int? CopyrightYear { get; set; }
    public bool? UsesHttps { get; set; }

    public OutreachStage Stage { get; set; } = OutreachStage.New;
    public string Notes { get; set; } = string.Empty;
    public DateTime? StageChangedAt { get; set; }

    /// <summary>
    /// Kiedy firma zgodziła się na przesłanie oferty – dowód na wypadek pytań (RODO art. 7 ust. 1:
    /// administrator musi umieć wykazać zgodę). Null = brak zgody, więc tylko wiadomości pierwszego kontaktu.
    /// </summary>
    public DateTime? ConsentGivenAt { get; set; }

    public DateTime FirstSeenAt { get; set; }
    public DateTime LastSeenAt { get; set; }

    /// <summary>Wyszukiwanie, w którym lead pojawił się pierwszy raz (filtr "nowe z wyszukiwania").</summary>
    public int FirstSearchRunId { get; set; }
    public int LastSearchRunId { get; set; }
}

/// <summary>Historia uruchomionych wyszukiwań.</summary>
public sealed class SearchRunEntity
{
    public int Id { get; set; }
    public required string City { get; set; }

    /// <summary>Frazy kategorii rozdzielone "|".</summary>
    public required string Categories { get; set; }

    public int Pages { get; set; }
    public SearchRunState State { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public int FoundCount { get; set; }
    public int NewCount { get; set; }
    public string? Error { get; set; }
}

/// <summary>Ustawienia aplikacji – jeden wiersz o Id = 1.</summary>
public sealed class AppSettingsEntity
{
    public const int SingletonId = 1;

    public int Id { get; set; } = SingletonId;

    /// <summary>
    /// Klucz wpisany w UI. Plik bazy leży w profilu użytkownika (%LOCALAPPDATA%), więc poziom ochrony
    /// jest taki sam jak pliku .env. Zmienna środowiskowa ma pierwszeństwo.
    /// </summary>
    public string? GooglePlacesApiKey { get; set; }

    public string? SenderName { get; set; }
    public string? Signature { get; set; }

    /// <summary>E-mail kontaktowy – w listach i klauzuli RODO.</summary>
    public string? ContactEmail { get; set; }

    /// <summary>Adres nadawcy – w nagłówku listu papierowego.</summary>
    public string? PostalAddress { get; set; }
}

/// <summary>
/// Firma, która nie życzy sobie kontaktu (sprzeciw z RODO art. 21). Przechowujemy wyłącznie place.id,
/// żeby przy kolejnych wyszukiwaniach nie wróciła jako "nowy lead" – reszta danych jest usuwana.
/// </summary>
public sealed class BlockedPlaceEntity
{
    public required string PlaceId { get; set; }
    public DateTime BlockedAt { get; set; }
}
