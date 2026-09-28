using LeadFinder.Models;
using LeadFinder.Services;
using LeadFinder.Web.Contracts;
using LeadFinder.Web.Data;
using LeadFinder.Web.Settings;
using Microsoft.EntityFrameworkCore;

namespace LeadFinder.Web.Endpoints;

public static class LeadEndpoints
{
    private const int MaxNotesLength = 10_000;

    public static void MapLeadEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/leads");
        group.MapGet("", ListAsync);
        group.MapPatch("/{id:int}", UpdateAsync);
        group.MapDelete("/{id:int}", DeleteAsync);
        group.MapPost("/export", ExportAsync);
        group.MapPost("/recheck-websites", RecheckWebsitesAsync);
    }

    /// <summary>Ile stron sprawdzamy równocześnie przy ponownym sprawdzaniu (każda i tak ma własny timeout).</summary>
    private const int RecheckParallelism = 6;

    /// <summary>
    /// Ponownie sprawdza strony wszystkich leadów, które mają własną stronę (nie działa / WordPress / ma stronę) –
    /// bez zapytań do Google, więc za darmo. Naprawia fałszywe "nie działa" z wcześniejszych skanów i ocenia
    /// nowoczesność stron sprawdzonych, zanim aplikacja to potrafiła. Etap, notatki i zgody zostają bez zmian.
    /// </summary>
    private static async Task<RecheckWebsitesResultDto> RecheckWebsitesAsync(
        LeadFinderDbContext db, WebsiteChecker checker, CancellationToken ct)
    {
        var leads = await db.Leads
            .Where(l => l.Status != LeadStatus.NoWebsite && l.WebsiteUri != null)
            .ToListAsync(ct);

        var checks = new System.Collections.Concurrent.ConcurrentDictionary<int, WebsiteCheckResult>();
        await Parallel.ForEachAsync(
            leads,
            new ParallelOptions { MaxDegreeOfParallelism = RecheckParallelism, CancellationToken = ct },
            async (lead, token) => checks[lead.Id] = await checker.CheckAsync(lead.WebsiteUri!, token));

        var before = leads.ToDictionary(l => l.Id, l => l.Status);
        foreach (var lead in leads)
        {
            var check = checks[lead.Id];
            lead.ApplyWebsiteCheck(check, LeadClassifier.Classify(lead.ToDomain().Place, check));
        }
        await db.SaveChangesAsync(ct);

        return new RecheckWebsitesResultDto(
            Checked: leads.Count,
            NowWorking: leads.Count(l => before[l.Id] == LeadStatus.WebsiteDown && l.Status != LeadStatus.WebsiteDown),
            StillDown: leads.Count(l => l.Status == LeadStatus.WebsiteDown),
            NoLongerWordPressTarget: leads.Count(l => before[l.Id] == LeadStatus.WordPress && l.Status == LeadStatus.HasWebsite));
    }

    /// <summary>
    /// Wszystkie leady naraz: przy lokalnej aplikacji to maks. kilka tysięcy rekordów, więc filtrowanie
    /// i sortowanie po stronie przeglądarki jest natychmiastowe i prostsze niż paginacja.
    /// </summary>
    private static async Task<IReadOnlyList<LeadDto>> ListAsync(
        LeadFinderDbContext db, SettingsService settings, CancellationToken ct)
    {
        var drafter = await settings.CreateMessageDrafterAsync(ct);
        var leads = await db.Leads.AsNoTracking().ToListAsync(ct);
        return leads
            .Select(l => l.ToDto(drafter))
            .OrderBy(l => l.Priority)
            .ThenByDescending(l => l.UserRatingCount ?? 0)
            .ToList();
    }

    private static async Task<IResult> UpdateAsync(
        int id, UpdateLeadRequest body, LeadFinderDbContext db, SettingsService settings, CancellationToken ct)
    {
        var lead = await db.Leads.FindAsync([id], ct);
        if (lead is null)
            return Results.NotFound();

        if (body.Notes is { Length: > MaxNotesLength })
            return Results.Problem($"Notatka może mieć maks. {MaxNotesLength} znaków.", statusCode: StatusCodes.Status400BadRequest);

        if (body.Stage is { } stage)
            SetStage(lead, stage);

        if (body.Notes is not null)
            lead.Notes = body.Notes;

        switch (body.ConsentGiven)
        {
            case true when lead.ConsentGivenAt is null:
                lead.ConsentGivenAt = DateTime.UtcNow;
                // Zgoda oznacza, że firma odpowiedziała – chyba że etap jest już dalej (np. "Klient").
                if (lead.Stage is OutreachStage.New or OutreachStage.Later or OutreachStage.Contacted)
                    SetStage(lead, OutreachStage.Replied);
                break;
            case false:
                lead.ConsentGivenAt = null;
                break;
        }

        if (body.ClearNextAction == true)
            lead.NextActionDate = null;
        else if (body.NextActionDate is { } nextAction)
            lead.NextActionDate = nextAction;

        await db.SaveChangesAsync(ct);
        return Results.Ok(lead.ToDto(await settings.CreateMessageDrafterAsync(ct)));
    }

    private static void SetStage(LeadEntity lead, OutreachStage stage)
    {
        if (stage == lead.Stage)
            return;

        lead.Stage = stage;
        lead.StageChangedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Usuwa lead. Z <c>?block=true</c> (sprzeciw firmy) zapamiętuje tylko place.id, żeby firma
    /// nie wróciła przy kolejnym wyszukiwaniu – wszystkie pozostałe dane są kasowane.
    /// </summary>
    private static async Task<IResult> DeleteAsync(int id, bool? block, LeadFinderDbContext db, CancellationToken ct)
    {
        var lead = await db.Leads.FindAsync([id], ct);
        if (lead is null)
            return Results.NotFound();

        if (block == true && await db.BlockedPlaces.FindAsync([lead.PlaceId], ct) is null)
            db.BlockedPlaces.Add(new BlockedPlaceEntity { PlaceId = lead.PlaceId, BlockedAt = DateTime.UtcNow });

        db.Leads.Remove(lead);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    /// <summary>Eksport wybranych leadów (np. aktualnie przefiltrowanych w UI) do CSV z Core.</summary>
    private static async Task<IResult> ExportAsync(
        ExportLeadsRequest body, LeadFinderDbContext db, SettingsService settings, CancellationToken ct)
    {
        var ids = body.Ids ?? [];
        if (ids.Count == 0)
            return Results.Problem("Brak leadów do eksportu.", statusCode: StatusCodes.Status400BadRequest);

        var leads = await db.Leads.AsNoTracking().Where(l => ids.Contains(l.Id)).ToListAsync(ct);
        var exporter = new CsvExporter(await settings.CreateMessageDrafterAsync(ct));
        var bytes = exporter.BuildCsvBytes(leads.Select(l => l.ToDomain()));

        var label = string.IsNullOrWhiteSpace(body.Label) ? "wybrane" : body.Label;
        return Results.File(bytes, "text/csv; charset=utf-8", CsvExporter.CreateFileName(label));
    }
}
