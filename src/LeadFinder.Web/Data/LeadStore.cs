using LeadFinder.Models;
using Microsoft.EntityFrameworkCore;

namespace LeadFinder.Web.Data;

/// <summary>Zapis wyników wyszukiwania do bazy.</summary>
public sealed class LeadStore(LeadFinderDbContext db)
{
    /// <summary>
    /// Dodaje nowe leady i odświeża istniejące (po place.id). Istniejącym leadom zostają
    /// etap CRM, notatki, zgoda, pierwotna kategoria i miasto. Firmy z listy sprzeciwów są pomijane.
    /// Zmiany trzeba zatwierdzić przez SaveChangesAsync.
    /// </summary>
    /// <returns>Liczba nowych i zaktualizowanych leadów.</returns>
    public async Task<(int Added, int Updated)> UpsertAsync(
        IReadOnlyList<Lead> leads, int searchRunId, CancellationToken cancellationToken = default)
    {
        var placeIds = leads.Select(l => l.Place.Id).ToList();
        var blocked = await db.BlockedPlaces
            .Where(b => placeIds.Contains(b.PlaceId))
            .Select(b => b.PlaceId)
            .ToListAsync(cancellationToken);
        leads = leads.Where(l => !blocked.Contains(l.Place.Id)).ToList();

        var existing = await db.Leads
            .Where(l => placeIds.Contains(l.PlaceId))
            .ToDictionaryAsync(l => l.PlaceId, cancellationToken);

        var now = DateTime.UtcNow;
        var added = 0;

        foreach (var lead in leads)
        {
            if (!existing.TryGetValue(lead.Place.Id, out var entity))
            {
                entity = new LeadEntity
                {
                    PlaceId = lead.Place.Id,
                    Name = lead.Place.Name,
                    City = lead.City,
                    CategoryId = lead.Category.Id,
                    CategoryQuery = lead.Category.Query,
                    CategoryTone = lead.Category.Tone,
                    FirstSeenAt = now,
                    FirstSearchRunId = searchRunId,
                };
                db.Leads.Add(entity);
                added++;
            }

            entity.ApplySearchResult(lead);
            entity.LastSeenAt = now;
            entity.LastSearchRunId = searchRunId;
        }

        return (added, leads.Count - added);
    }
}
