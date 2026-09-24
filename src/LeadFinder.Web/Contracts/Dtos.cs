using LeadFinder.Models;
using LeadFinder.Web.Data;

namespace LeadFinder.Web.Contracts;

// Kontrakty HTTP API. Enumy serializowane jako tekst (JsonStringEnumConverter w Program.cs),
// typy po stronie frontendu: ClientApp/src/types.ts.

public sealed record LeadDto(
    int Id,
    string PlaceId,
    string Name,
    string? Address,
    string? Phone,
    string? WebsiteUri,
    double? Rating,
    int? UserRatingCount,
    string City,
    string CategoryId,
    string CategoryName,
    LeadStatus Status,
    string StatusLabel,
    int Priority,
    string? CheckNote,
    string? Technology,
    string? ProfilePlatform,
    OutreachStage Stage,
    string Notes,
    DateTime? StageChangedAt,
    DateTime? ConsentGivenAt,
    DateOnly? NextActionDate,
    DateTime FirstSeenAt,
    DateTime LastSeenAt,
    int FirstSearchRunId,
    int LastSearchRunId,
    IReadOnlyList<MessageDraft> Drafts,
    LeadScore Score,
    string? BusinessStatus,
    string GoogleMapsUrl);

/// <summary>
/// Częściowa aktualizacja: null = bez zmian. ConsentGiven=true zapisuje datę zgody
/// (i przesuwa etap na "Odpowiedział"), false ją usuwa. ClearNextAction=true usuwa przypomnienie.
/// </summary>
public sealed record UpdateLeadRequest(
    OutreachStage? Stage, string? Notes, bool? ConsentGiven, DateOnly? NextActionDate, bool? ClearNextAction);

public sealed record ExportLeadsRequest(IReadOnlyList<int>? Ids, string? Label);

/// <summary>
/// Wyszukiwanie w jednym albo wielu miastach (skan województwa / Polski). Label – nazwa do historii,
/// np. "Katowice" albo "śląskie". AcceptOverLimit – zgoda na przekroczenie darmowego limitu Google.
/// </summary>
public sealed record StartSearchRequest(
    string? Label,
    IReadOnlyList<string>? Cities,
    IReadOnlyList<string>? CategoryIds,
    int? Pages,
    int? MinScore,
    bool? AcceptOverLimit);

public sealed record SearchRunDto(
    int Id,
    string City,
    IReadOnlyList<string> Categories,
    int Pages,
    int CityCount,
    int MinScore,
    int? ApiRequests,
    SearchRunState State,
    DateTime StartedAt,
    DateTime? FinishedAt,
    int FoundCount,
    int NewCount,
    string? Error);

public sealed record CurrentSearchDto(SearchRunDto Run, bool IsRunning);

public static class SearchEventTypes
{
    public const string Progress = "progress";
    public const string Completed = "completed";
    public const string Failed = "failed";
    public const string Cancelled = "cancelled";
}

/// <summary>Zdarzenie wysyłane przez SSE. Zdarzenia inne niż "progress" kończą strumień i niosą stan wyszukiwania.</summary>
public sealed record SearchEventDto(
    string Type,
    SearchStage? Stage,
    string Message,
    int Current,
    int Total,
    DateTime At,
    SearchRunDto? Run = null);

public sealed record CategoryDto(string Id, string Query, string EnglishName);

public sealed record RegionDto(string Id, string Name, IReadOnlyList<string> Cities);

/// <summary>
/// Szacunkowe zużycie darmowego limitu Google w bieżącym miesiącu – liczone przez aplikację, bo Google
/// nie udostępnia go przez sam klucz API. IncludesEstimates: część starszych wyszukiwań jest oszacowana.
/// </summary>
public sealed record UsageDto(string Month, int Used, int Limit, int Remaining, bool IncludesEstimates);

public enum ApiKeySource
{
    None,
    Environment,
    App,
}

public sealed record SettingsDto(
    bool HasApiKey,
    ApiKeySource ApiKeySource,
    string? ApiKeyHint,
    string SenderName,
    string Signature,
    string ContactEmail,
    string PostalAddress,
    int FreeMonthlyRequests,
    string DefaultSenderName,
    string DefaultSignature,
    string DataDirectory);

/// <summary>Wszystkie pola: null = bez zmian. ApiKey "" = usuń klucz zapisany w aplikacji.</summary>
public sealed record UpdateSettingsRequest(
    string? ApiKey,
    string? SenderName,
    string? Signature,
    string? ContactEmail,
    string? PostalAddress,
    int? FreeMonthlyRequests);

public sealed record VerifyApiKeyResultDto(bool Ok, string Message);
