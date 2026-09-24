using LeadFinder.Models;

namespace LeadFinder.Services;

/// <summary>Parametry jednego wyszukiwania (jednego miasta).</summary>
/// <param name="City">Miasto dopisywane do frazy: "{kategoria} {miasto}".</param>
/// <param name="Categories">Kategorie do przeszukania.</param>
/// <param name="MaxPagesPerCategory">Maks. stron wyników na kategorię (1 strona = 1 płatne zapytanie).</param>
/// <param name="MinScore">Zwracaj tylko leady z szansą co najmniej tyle (0 = wszystkie).</param>
/// <param name="SkipPlaceIds">
/// Firmy już przetworzone (np. w poprzednim mieście tego samego skanu województwa) – pomijane,
/// żeby nie sprawdzać drugi raz tej samej strony.
/// </param>
public sealed record LeadSearchRequest(
    string City,
    IReadOnlyList<Category> Categories,
    int MaxPagesPerCategory,
    int MinScore = 0,
    IReadOnlySet<string>? SkipPlaceIds = null);

/// <summary>
/// Spina proces wyszukiwania: Google Places → deduplikacja → sprawdzanie stron → klasyfikacja.
/// Nie wie nic o konsoli, bazie ani CSV: postęp raportuje przez <see cref="IProgress{T}"/>,
/// a wynik zwraca wywołującemu (CLI zapisuje go do CSV, aplikacja webowa do bazy).
/// </summary>
public sealed class LeadSearchPipeline
{
    /// <summary>
    /// Przerwa między requestami do stron biznesów: nie zapycha łącza i nie obciąża
    /// małych stron na tanich hostingach serią szybkich zapytań.
    /// </summary>
    public static readonly TimeSpan WebsiteRequestDelay = TimeSpan.FromMilliseconds(300);

    private readonly PlacesApiClient _placesClient;
    private readonly WebsiteChecker _websiteChecker;

    public LeadSearchPipeline(PlacesApiClient placesClient, WebsiteChecker websiteChecker)
    {
        _placesClient = placesClient;
        _websiteChecker = websiteChecker;
    }

    /// <summary>Wykonuje pełne wyszukiwanie i zwraca sklasyfikowane leady (bez sortowania).</summary>
    /// <param name="request">Miasto, kategorie i limit stron.</param>
    /// <param name="progress">Odbiorca zdarzeń postępu (opcjonalny).</param>
    /// <param name="cancellationToken">Anulowanie wyszukiwania.</param>
    /// <exception cref="PlacesApiException">Błąd Google Places API – przerywa całe wyszukiwanie.</exception>
    public async Task<IReadOnlyList<Lead>> RunAsync(
        LeadSearchRequest request, IProgress<SearchProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var places = await CollectPlacesAsync(request, progress, cancellationToken);

        if (request.SkipPlaceIds is { Count: > 0 } skip)
            places.RemoveAll(p => skip.Contains(p.Place.Id));

        // Próg szansy: firmy, które nawet w najlepszym razie nie przekroczą progu, odpadają przed
        // sprawdzaniem stron – przy skanie województwa to oszczędza godziny.
        if (request.MinScore > 0)
        {
            var before = places.Count;
            places.RemoveAll(p => LeadScorer.PotentialScore(p.Place, p.Category) < request.MinScore);
            progress?.Report(new SearchProgress(
                SearchStage.Searching,
                $"Próg szansy {request.MinScore}+: {before - places.Count} firm bez szans pominiętych, {places.Count} do sprawdzenia"));
        }

        var websiteChecks = await CheckWebsitesAsync(places.Select(p => p.Place), progress, cancellationToken);

        return places
            .Select(found =>
            {
                var check = found.Place.WebsiteUri is { } uri ? websiteChecks.GetValueOrDefault(uri) : null;
                var status = LeadClassifier.Classify(found.Place, check);
                return new Lead(found.Place, found.Category, request.City, status, check);
            })
            .Where(lead => request.MinScore == 0 || LeadScorer.Score(lead).Value >= request.MinScore)
            .ToList();
    }

    /// <summary>
    /// Wyszukuje kolejne kategorie i deduplikuje wyniki po place.id: salon "fryzjer &amp; kosmetyka"
    /// wyjdzie zarówno dla "salon fryzjerski", jak i "salon kosmetyczny". Zostaje pierwsza kategoria.
    /// </summary>
    private async Task<List<(Place Place, Category Category)>> CollectPlacesAsync(
        LeadSearchRequest request, IProgress<SearchProgress>? progress, CancellationToken cancellationToken)
    {
        var seenIds = new HashSet<string>();
        var result = new List<(Place, Category)>();
        var total = request.Categories.Count;

        for (var i = 0; i < total; i++)
        {
            var category = request.Categories[i];
            var query = $"{category.Query} {request.City}";

            var found = await _placesClient.SearchTextAsync(query, request.MaxPagesPerCategory, cancellationToken);
            var added = 0;
            foreach (var place in found.Where(p => seenIds.Add(p.Id)))
            {
                result.Add((place, category));
                added++;
            }

            progress?.Report(new SearchProgress(
                SearchStage.Searching, $"\"{query}\": {found.Count} wyników, {added} nowych", i + 1, total));
        }

        progress?.Report(new SearchProgress(SearchStage.Searching, $"Unikalnych biznesów: {result.Count}"));
        return result;
    }

    /// <summary>
    /// Sprawdza strony po kolei, z przerwą między requestami. Ten sam adres (np. sieć salonów)
    /// sprawdzany jest tylko raz.
    /// </summary>
    private async Task<Dictionary<string, WebsiteCheckResult>> CheckWebsitesAsync(
        IEnumerable<Place> places, IProgress<SearchProgress>? progress, CancellationToken cancellationToken)
    {
        var uniqueUris = places
            .Select(p => p.WebsiteUri)
            .OfType<string>()
            .Where(uri => !string.IsNullOrWhiteSpace(uri))
            .Distinct()
            .ToList();

        var results = new Dictionary<string, WebsiteCheckResult>();
        if (uniqueUris.Count == 0)
            return results;

        progress?.Report(new SearchProgress(
            SearchStage.CheckingWebsites,
            $"Sprawdzam strony WWW ({uniqueUris.Count}), limit {WebsiteChecker.DefaultTimeout.TotalSeconds:0} s na stronę",
            0, uniqueUris.Count));

        for (var i = 0; i < uniqueUris.Count; i++)
        {
            if (i > 0)
                await Task.Delay(WebsiteRequestDelay, cancellationToken);

            var check = await _websiteChecker.CheckAsync(uniqueUris[i], cancellationToken);
            results[uniqueUris[i]] = check;

            progress?.Report(new SearchProgress(
                SearchStage.CheckingWebsites, $"{uniqueUris[i]} → {check.Note}", i + 1, uniqueUris.Count));
        }

        return results;
    }
}
