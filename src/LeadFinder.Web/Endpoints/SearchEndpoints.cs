using System.Text.Json;
using LeadFinder.Models;
using LeadFinder.Services;
using LeadFinder.Web.Contracts;
using LeadFinder.Web.Data;
using LeadFinder.Web.Search;
using LeadFinder.Web.Settings;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using LeadFinder.Config;

namespace LeadFinder.Web.Endpoints;

public static class SearchEndpoints
{
    /// <summary>Google zwraca maks. 60 wyników (3 strony) na frazę – więcej stron nic nie daje.</summary>
    private const int MaxPages = 3;
    private const int MaxCityLength = 100;

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
        SearchJobRunner runner,
        CancellationToken ct)
    {
        var city = body.City?.Trim();
        if (string.IsNullOrEmpty(city) || city.Length > MaxCityLength)
            return Results.Problem("Podaj nazwę miasta.", statusCode: StatusCodes.Status400BadRequest);

        var pages = body.Pages ?? MaxPages;
        if (pages is < 1 or > MaxPages)
            return Results.Problem($"Liczba stron musi być od 1 do {MaxPages}.", statusCode: StatusCodes.Status400BadRequest);

        IReadOnlyList<Category> categories;
        if (body.CategoryIds is null || body.CategoryIds.Count == 0)
        {
            categories = catalog.Categories;
        }
        else
        {
            var unknown = body.CategoryIds.Where(id => catalog.Categories.All(c => c.Id != id)).ToList();
            if (unknown.Count > 0)
                return Results.Problem($"Nieznane kategorie: {string.Join(", ", unknown)}.", statusCode: StatusCodes.Status400BadRequest);

            // Kolejność z pliku konfiguracyjnego, nie z kliknięć – o kategorii leada decyduje pierwsze trafienie.
            categories = catalog.Categories.Where(c => body.CategoryIds.Contains(c.Id)).ToList();
        }

        var (apiKey, _) = await settings.GetApiKeyAsync(ct);
        if (apiKey is null)
            return Results.Problem("Brak klucza Google Places API. Dodaj go w Ustawieniach.", statusCode: StatusCodes.Status400BadRequest);

        try
        {
            var run = await runner.StartAsync(new LeadSearchRequest(city, categories, pages), apiKey, ct);
            return Results.Accepted($"/api/searches/{run.Id}", run);
        }
        catch (SearchAlreadyRunningException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status409Conflict);
        }
    }

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
