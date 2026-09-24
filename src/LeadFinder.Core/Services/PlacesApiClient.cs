using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using LeadFinder.Models;

namespace LeadFinder.Services;

/// <summary>
/// Klient endpointu Text Search z Google Places API (New).
/// Dokumentacja: https://developers.google.com/maps/documentation/places/web-service/text-search
/// </summary>
public sealed class PlacesApiClient
{
    private const string SearchTextEndpoint = "https://places.googleapis.com/v1/places:searchText";

    /// <summary>
    /// Pola, które Google ma zwrócić. Od nich zależy cena zapytania (SKU), więc prosimy tylko o potrzebne.
    /// Pole "nextPageToken" też musi tu być: bez niego Google nie zwróci tokenu kolejnej strony.
    /// businessStatus należy do tańszego SKU niż websiteUri/rating, więc nie podnosi ceny zapytania
    /// (płaci się za najdroższe pole z maski).
    /// </summary>
    private const string FieldMask =
        "places.id,places.displayName,places.formattedAddress,places.nationalPhoneNumber," +
        "places.websiteUri,places.rating,places.userRatingCount,places.businessStatus,nextPageToken";

    /// <summary>Maksymalny rozmiar strony w Text Search.</summary>
    private const int PageSize = 20;

    /// <summary>
    /// Świeżo wydany nextPageToken zaczyna działać dopiero po chwili. Wcześniejsze użycie kończy się
    /// błędem 400 (INVALID_ARGUMENT), dlatego czekamy przed każdą kolejną stroną.
    /// </summary>
    private static readonly TimeSpan PageTokenDelay = TimeSpan.FromSeconds(2);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, // brak pageToken na 1. stronie
    };

    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _languageCode;
    private readonly string _regionCode;

    /// <param name="httpClient">Współdzielony klient HTTP (czas życia zarządzany przez wywołującego).</param>
    /// <param name="apiKey">Klucz Google Places API.</param>
    /// <param name="languageCode">Język wyników, np. nazwy i adresy.</param>
    /// <param name="regionCode">Region (CLDR), wpływa na formatowanie adresów i ranking wyników.</param>
    public PlacesApiClient(HttpClient httpClient, string apiKey, string languageCode = "pl", string regionCode = "PL")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        _httpClient = httpClient;
        _apiKey = apiKey;
        _languageCode = languageCode;
        _regionCode = regionCode;
    }

    /// <summary>
    /// Wyszukuje miejsca dla frazy i pobiera kolejne strony wyników, dopóki Google zwraca nextPageToken
    /// albo do osiągnięcia <paramref name="maxPages"/>. Google zwraca maksymalnie 60 wyników (3 strony) na frazę.
    /// </summary>
    /// <param name="textQuery">Zapytanie, np. "barber shop Katowice".</param>
    /// <param name="maxPages">Maksymalna liczba stron (jedna strona = do 20 wyników = jedno płatne zapytanie).</param>
    /// <param name="cancellationToken">Anulowanie (Ctrl+C).</param>
    /// <exception cref="PlacesApiException">Błąd API (zły klucz, brak uprawnień, limit) lub sieci.</exception>
    public async Task<IReadOnlyList<Place>> SearchTextAsync(
        string textQuery, int maxPages, CancellationToken cancellationToken = default)
    {
        var places = new List<Place>();
        string? pageToken = null;

        for (var page = 1; page <= maxPages; page++)
        {
            if (pageToken is not null)
                await Task.Delay(PageTokenDelay, cancellationToken);

            var response = await SendSearchRequestAsync(textQuery, pageToken, cancellationToken);
            places.AddRange((response.Places ?? []).Select(MapPlace).OfType<Place>());

            pageToken = response.NextPageToken;
            if (string.IsNullOrEmpty(pageToken))
                break; // to była ostatnia strona
        }

        return places;
    }

    /// <summary>
    /// Sprawdza, czy klucz działa, jednym zapytaniem proszącym wyłącznie o place.id.
    /// Taki FieldMask to SKU "Text Search Essentials (IDs Only)", który według cennika Google jest bezpłatny.
    /// </summary>
    /// <exception cref="PlacesApiException">Klucz nie działa – komunikat mówi dlaczego.</exception>
    public async Task VerifyApiKeyAsync(CancellationToken cancellationToken = default) =>
        await SendSearchRequestAsync("fryzjer Warszawa", pageToken: null, cancellationToken, fieldMask: "places.id", pageSize: 1);

    private async Task<SearchTextResponse> SendSearchRequestAsync(
        string textQuery, string? pageToken, CancellationToken cancellationToken,
        string fieldMask = FieldMask, int pageSize = PageSize)
    {
        // Przy stronicowaniu Google wymaga, by pozostałe parametry były identyczne jak w pierwszym zapytaniu.
        var body = new SearchTextRequest(textQuery, _languageCode, _regionCode, pageSize, pageToken);

        for (var attempt = 1; ; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, SearchTextEndpoint)
            {
                Content = JsonContent.Create(body, options: JsonOptions),
            };
            request.Headers.Add("X-Goog-Api-Key", _apiKey);
            request.Headers.Add("X-Goog-FieldMask", fieldMask);

            using var response = await SendAsync(request, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
                return JsonSerializer.Deserialize<SearchTextResponse>(json, JsonOptions) ?? new SearchTextResponse(null, null);

            // Token może jeszcze "nie dojrzeć" mimo odczekania 2s: jedna ponowna próba z dodatkowym opóźnieniem.
            // Pierwsza strona (bez tokenu) nigdy nie jest ponawiana, więc np. zły klucz zgłosi się od razu.
            if (pageToken is not null && response.StatusCode == HttpStatusCode.BadRequest && attempt == 1)
            {
                await Task.Delay(PageTokenDelay, cancellationToken);
                continue;
            }

            throw PlacesApiException.FromErrorResponse(response.StatusCode, json);
        }
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            return await _httpClient.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new PlacesApiException(
                $"Nie udało się połączyć z Google Places API ({ex.Message}). Sprawdź połączenie z internetem.", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new PlacesApiException("Google Places API nie odpowiedziało w wyznaczonym czasie.", ex);
        }
    }

    private static Place? MapPlace(PlaceDto dto) =>
        string.IsNullOrEmpty(dto.Id)
            ? null
            : new Place(
                Id: dto.Id,
                Name: dto.DisplayName?.Text ?? "(bez nazwy)",
                Address: dto.FormattedAddress,
                Phone: dto.NationalPhoneNumber,
                WebsiteUri: dto.WebsiteUri,
                Rating: dto.Rating,
                UserRatingCount: dto.UserRatingCount,
                BusinessStatus: dto.BusinessStatus);

    // --- Kształty JSON żądania i odpowiedzi (camelCase przez JsonSerializerDefaults.Web) ---

    private sealed record SearchTextRequest(
        string TextQuery, string LanguageCode, string RegionCode, int PageSize, string? PageToken);

    private sealed record SearchTextResponse(List<PlaceDto>? Places, string? NextPageToken);

    private sealed record PlaceDto(
        string? Id,
        LocalizedText? DisplayName,
        string? FormattedAddress,
        string? NationalPhoneNumber,
        string? WebsiteUri,
        double? Rating,
        int? UserRatingCount,
        string? BusinessStatus);

    private sealed record LocalizedText(string? Text, string? LanguageCode);
}
