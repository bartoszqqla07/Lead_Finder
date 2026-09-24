using LeadFinder.Common;
using LeadFinder.Models;
using LeadFinder.Services;
using LeadFinder.Web.Contracts;
using LeadFinder.Web.Data;

namespace LeadFinder.Web.Search;

public sealed class SearchAlreadyRunningException() : Exception("Inne wyszukiwanie jest już w toku.");

/// <summary>Wyszukiwanie w jednym albo wielu miastach (skan województwa / Polski).</summary>
/// <param name="Label">Nazwa do historii, np. "Katowice" albo "śląskie".</param>
/// <param name="Cities">Miasta do przeszukania, po kolei.</param>
/// <param name="Categories">Kategorie.</param>
/// <param name="Pages">Stron wyników na kategorię.</param>
/// <param name="MinScore">Zapisuj tylko leady z szansą co najmniej tyle (0 = wszystkie).</param>
public sealed record RegionSearchRequest(
    string Label, IReadOnlyList<string> Cities, IReadOnlyList<Category> Categories, int Pages, int MinScore);

/// <summary>
/// Uruchamia wyszukiwania w tle – niezależnie od requestu HTTP, więc zamknięcie karty przeglądarki
/// nie przerywa pracy. Naraz działa jedno wyszukiwanie: chroni to limity Google i łącze.
/// </summary>
public sealed class SearchJobRunner : IDisposable
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly WebsiteChecker _websiteChecker;
    private readonly ILogger<SearchJobRunner> _logger;
    private readonly HttpClient _placesHttpClient = new() { Timeout = TimeSpan.FromSeconds(30) };
    private readonly SemaphoreSlim _startLock = new(1, 1);
    private volatile SearchJob? _current;

    public SearchJobRunner(IServiceScopeFactory scopeFactory, WebsiteChecker websiteChecker, ILogger<SearchJobRunner> logger)
    {
        _scopeFactory = scopeFactory;
        _websiteChecker = websiteChecker;
        _logger = logger;
    }

    /// <summary>Ostatnie wyszukiwanie uruchomione od startu aplikacji (trwające albo zakończone).</summary>
    public SearchJob? Current => _current;

    /// <summary>Zapisuje wyszukiwanie w historii i uruchamia je w tle.</summary>
    /// <exception cref="SearchAlreadyRunningException">Inne wyszukiwanie jeszcze trwa.</exception>
    public async Task<SearchRunDto> StartAsync(RegionSearchRequest request, string apiKey, CancellationToken cancellationToken = default)
    {
        // Semafor zamiast lock: w środku jest await (zapis do bazy), a dwa równoległe
        // kliknięcia "Szukaj" nie mogą uruchomić dwóch wyszukiwań.
        await _startLock.WaitAsync(cancellationToken);
        try
        {
            if (_current is { IsFinished: false })
                throw new SearchAlreadyRunningException();

            SearchRunEntity run;
            using (var scope = _scopeFactory.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<LeadFinderDbContext>();
                run = new SearchRunEntity
                {
                    City = request.Label,
                    Categories = string.Join('|', request.Categories.Select(c => c.Query)),
                    Pages = request.Pages,
                    CityCount = request.Cities.Count,
                    MinScore = request.MinScore,
                    ApiRequests = 0,
                    State = SearchRunState.Running,
                    StartedAt = DateTime.UtcNow,
                };
                db.SearchRuns.Add(run);
                await db.SaveChangesAsync(cancellationToken);
            }

            var job = new SearchJob(run.Id);
            _current = job;
            _ = Task.Run(() => ExecuteAsync(job, request, apiKey), CancellationToken.None);
            return run.ToDto();
        }
        finally
        {
            _startLock.Release();
        }
    }

    /// <summary>Anuluje trwające wyszukiwanie o podanym Id.</summary>
    /// <returns>false, gdy takie wyszukiwanie nie trwa.</returns>
    public bool TryCancel(int searchRunId)
    {
        var job = _current;
        if (job is null || job.SearchRunId != searchRunId || job.IsFinished)
            return false;

        job.Cancellation.Cancel();
        return true;
    }

    /// <summary>
    /// Przechodzi miasto po mieście i zapisuje wyniki po każdym z nich: przerwanie długiego skanu
    /// (albo wyczerpanie limitu Google w połowie) nie przepala już zużytych zapytań.
    /// </summary>
    private async Task ExecuteAsync(SearchJob job, RegionSearchRequest request, string apiKey)
    {
        var token = job.Cancellation.Token;
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LeadFinderDbContext>();
        var run = await db.SearchRuns.FindAsync(job.SearchRunId)
            ?? throw new InvalidOperationException($"Brak wyszukiwania {job.SearchRunId} w bazie.");

        var placesClient = new PlacesApiClient(_placesHttpClient, apiKey);
        var pipeline = new LeadSearchPipeline(placesClient, _websiteChecker);
        var store = new LeadStore(db);
        var processedPlaceIds = new HashSet<string>();
        var citiesDone = 0;

        string eventType;
        string message;
        try
        {
            var cities = request.Cities;
            for (var i = 0; i < cities.Count; i++)
            {
                if (cities.Count > 1)
                    job.Report(new SearchProgress(SearchStage.Region, $"Miasto {i + 1}/{cities.Count}: {cities[i]}", i + 1, cities.Count));

                var cityRequest = new LeadSearchRequest(
                    cities[i], request.Categories, request.Pages, request.MinScore, processedPlaceIds);
                var leads = await pipeline.RunAsync(cityRequest, job, token);

                var (added, updated) = await store.UpsertAsync(leads, run.Id, CancellationToken.None);
                processedPlaceIds.UnionWith(leads.Select(l => l.Place.Id));
                run.FoundCount += added + updated; // bez firm z listy sprzeciwów
                run.NewCount += added;
                run.ApiRequests = placesClient.BillableRequestCount;
                await db.SaveChangesAsync(CancellationToken.None);
                citiesDone++;
            }

            run.State = SearchRunState.Completed;
            (eventType, message) = (SearchEventTypes.Completed, $"Gotowe: {run.FoundCount} leadów, w tym {run.NewCount} nowych.");
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            run.State = SearchRunState.Cancelled;
            (eventType, message) = (SearchEventTypes.Cancelled, request.Cities.Count > 1
                ? $"Przerwano. Zapisano wyniki z {citiesDone} z {request.Cities.Count} miast ({run.NewCount} nowych leadów)."
                : "Przerwano. Wyniki tego wyszukiwania nie zostały zapisane.");
        }
        catch (LeadFinderException ex)
        {
            // Błędy Google Places API (zły klucz, limit) – komunikat jest już zrozumiały dla użytkownika.
            run.State = SearchRunState.Failed;
            run.Error = citiesDone > 0 ? $"{ex.Message} (zapisano wyniki z {citiesDone} miast)" : ex.Message;
            (eventType, message) = (SearchEventTypes.Failed, run.Error);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Wyszukiwanie {SearchRunId} zakończyło się nieoczekiwanym błędem", run.Id);
            run.State = SearchRunState.Failed;
            run.Error = $"Nieoczekiwany błąd: {ex.Message}";
            (eventType, message) = (SearchEventTypes.Failed, run.Error);
        }

        run.ApiRequests = placesClient.BillableRequestCount;
        run.FinishedAt = DateTime.UtcNow;
        try
        {
            await db.SaveChangesAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Nie udało się zapisać wyniku wyszukiwania {SearchRunId}", run.Id);
            (eventType, message) = (SearchEventTypes.Failed, $"Nie udało się zapisać wyników do bazy: {ex.Message}");
            run.State = SearchRunState.Failed;
        }

        job.Finish(eventType, message, run.ToDto());
    }

    public void Dispose()
    {
        _current?.Cancellation.Cancel();
        _placesHttpClient.Dispose();
        _startLock.Dispose();
    }
}
