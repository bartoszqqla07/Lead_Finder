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
                if (lead.Stage is OutreachStage.New or OutreachStage.Contacted)
                    SetStage(lead, OutreachStage.Replied);
                break;
            case false:
                lead.ConsentGivenAt = null;
                break;
        }

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
