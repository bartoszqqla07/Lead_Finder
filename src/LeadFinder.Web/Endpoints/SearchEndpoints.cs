using System.Text.Json;
using LeadFinder.Config;
using LeadFinder.Models;
using LeadFinder.Services;
using LeadFinder.Web.Contracts;
using LeadFinder.Web.Data;
using LeadFinder.Web.Search;
using LeadFinder.Web.Settings;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LeadFinder.Web.Endpoints;

public static class SearchEndpoints
{
    /// <summary>Google zwraca maks. 60 wyników (3 strony) na frazę – więcej stron nic nie daje.</summary>
    private const int MaxPages = 3;
    private const int MaxCityLength = 100;

    /// <summary>Górna granica miast w jednym skanie (cała Polska z regions.json to ok. 170).</summary>
    private const int MaxCities = 300;

    public static void MapSearchEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/searches");
        group.MapGet("", ListAsync);
        group.MapPost("", StartAsync);
        group.MapGet("/current", GetCurrentAsync);
        group.MapGet("/{id:int}/events", StreamEventsAsync);
        group.MapPost("/{id:int}/cancel", Cancel);
    }

    private static async Task<IReadOnlyList<SearchRunDto>> ListAsync(LeadFinderDbContext db, CancellationToken ct)
    {
        var runs = await db.SearchRuns.OrderByDescending(r => r.Id).Take(30).ToListAsync(ct);
        return runs.Select(r => r.ToDto()).ToList();
    }

    private static async Task<IResult> StartAsync(
        StartSearchRequest body,
        CategoryCatalog catalog,
        SettingsService settings,
        UsageService usage,
        SearchJobRunner runner,
        CancellationToken ct)
    {
        var cities = (body.Cities ?? [])
            .Select(c => c.Trim())
            .Where(c => c.Length is > 0 and <= MaxCityLength)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (cities.Count == 0)
            return Problem("Podaj miasto albo wybierz województwo.");
        if (cities.Count > MaxCities)
            return Problem($"Jednorazowo można przeszukać maks. {MaxCities} miast.");

        var pages = body.Pages ?? MaxPages;
        if (pages is < 1 or > MaxPages)
            return Problem($"Liczba stron musi być od 1 do {MaxPages}.");

        var minScore = body.MinScore ?? 0;
        if (minScore is < 0 or > 100)
            return Problem("Próg szansy musi być od 0 do 100.");

        IReadOnlyList<Category> categories;
        if (body.CategoryIds is null || body.CategoryIds.Count == 0)
        {
            categories = catalog.Categories;
        }
        else
        {
            var unknown = body.CategoryIds.Where(id => catalog.Categories.All(c => c.Id != id)).ToList();
            if (unknown.Count > 0)
                return Problem($"Nieznane kategorie: {string.Join(", ", unknown)}.");

            // Kolejność z pliku konfiguracyjnego, nie z kliknięć – o kategorii leada decyduje pierwsze trafienie.
            categories = catalog.Categories.Where(c => body.CategoryIds.Contains(c.Id)).ToList();
        }

        var (apiKey, _) = await settings.GetApiKeyAsync(ct);
        if (apiKey is null)
            return Problem("Brak klucza Google Places API. Dodaj go w Ustawieniach.");

        // Bezpiecznik kosztów: bez wyraźnej zgody nie zaczynamy skanu, który może wyjść poza darmowy limit.
        var maxRequests = cities.Count * categories.Count * pages;
        var monthUsage = await usage.GetCurrentMonthAsync(ct);
        if (maxRequests > monthUsage.Remaining && body.AcceptOverLimit != true)
        {
            return Problem(
                $"To wyszukiwanie może wysłać do {maxRequests} płatnych zapytań, a w darmowym limicie zostało ok. " +
                $"{monthUsage.Remaining}. Zmniejsz zakres albo potwierdź przekroczenie limitu.",
                StatusCodes.Status409Conflict);
        }

        var label = string.IsNullOrWhiteSpace(body.Label) ? cities[0] : body.Label.Trim();
        try
        {
            var run = await runner.StartAsync(new RegionSearchRequest(label, cities, categories, pages, minScore), apiKey, ct);
            return Results.Accepted($"/api/searches/{run.Id}", run);
        }
        catch (SearchAlreadyRunningException ex)
        {
            return Problem(ex.Message, StatusCodes.Status409Conflict);
        }
    }

    private static IResult Problem(string detail, int statusCode = StatusCodes.Status400BadRequest) =>
        Results.Problem(detail, statusCode: statusCode);

    private static async Task<IResult> GetCurrentAsync(SearchJobRunner runner, LeadFinderDbContext db, CancellationToken ct)
    {
        var job = runner.Current;
        if (job is null)
            return Results.NoContent();

        var run = await db.SearchRuns.FindAsync([job.SearchRunId], ct);
        return run is null ? Results.NoContent() : Results.Ok(new CurrentSearchDto(run.ToDto(), !job.IsFinished));
    }

    /// <summary>
    /// Server-Sent Events: najpierw cała dotychczasowa historia zdarzeń, potem zdarzenia na żywo.
    /// Strumień kończy się zdarzeniem "completed" / "failed" / "cancelled".
    /// </summary>
    private static async Task StreamEventsAsync(
        int id, HttpContext http, SearchJobRunner runner, IOptions<JsonOptions> jsonOptions, CancellationToken ct)
    {
        var job = runner.Current;
        if (job is null || job.SearchRunId != id)
        {
            http.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        http.Response.ContentType = "text/event-stream";
        http.Response.Headers.CacheControl = "no-cache";
        http.Response.Headers["X-Accel-Buffering"] = "no";

        var serializerOptions = jsonOptions.Value.SerializerOptions;
        using var subscription = job.Subscribe();

        try
        {
            foreach (var @event in subscription.History)
                await WriteEventAsync(@event);

            if (subscription.Live is not null)
            {
                await foreach (var @event in subscription.Live.ReadAllAsync(ct))
                    await WriteEventAsync(@event);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Przeglądarka zamknęła połączenie – to normalne.
        }

        async Task WriteEventAsync(SearchEventDto @event)
        {
            await http.Response.WriteAsync($"data: {JsonSerializer.Serialize(@event, serializerOptions)}\n\n", ct);
            await http.Response.Body.FlushAsync(ct);
        }
    }

    private static IResult Cancel(int id, SearchJobRunner runner) =>
        runner.TryCancel(id)
            ? Results.Accepted()
            : Results.Problem("To wyszukiwanie nie jest w toku.", statusCode: StatusCodes.Status409Conflict);
}
