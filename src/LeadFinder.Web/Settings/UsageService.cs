using LeadFinder.Web.Contracts;
using LeadFinder.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace LeadFinder.Web.Settings;

/// <summary>
/// Szacuje zużycie darmowego miesięcznego limitu Google Places w bieżącym miesiącu.
/// </summary>
/// <remarks>
/// Google nie udostępnia zużycia przez sam klucz API (trzeba by osobnego dostępu do Cloud Monitoring),
/// więc aplikacja liczy płatne zapytania sama – przy każdym wyszukiwaniu (<see cref="SearchRunEntity.ApiRequests"/>).
/// Wyszukiwania sprzed wprowadzenia licznika liczone są z górą: kategorie × strony × miasta.
/// Nie widzimy zapytań z innych aplikacji ani komputerów używających tego samego klucza.
/// </remarks>
public sealed class UsageService(LeadFinderDbContext db, SettingsService settings)
{
    /// <summary>Zużycie w bieżącym miesiącu kalendarzowym (Google rozlicza limity miesięcznie).</summary>
    public async Task<UsageDto> GetCurrentMonthAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var runs = await db.SearchRuns.AsNoTracking()
            .Where(r => r.StartedAt >= monthStart)
            .Select(r => new { r.ApiRequests, r.Categories, r.Pages, r.CityCount })
            .ToListAsync(cancellationToken);

        var used = runs.Sum(r => r.ApiRequests ?? EstimateLegacy(r.Categories, r.Pages, r.CityCount));
        var limit = (await settings.GetDtoAsync(cancellationToken)).FreeMonthlyRequests;

        return new UsageDto(
            Month: monthStart.ToString("yyyy-MM"),
            Used: used,
            Limit: limit,
            Remaining: Math.Max(0, limit - used),
            IncludesEstimates: runs.Any(r => r.ApiRequests is null));
    }

    /// <summary>Górna granica dla wyszukiwań sprzed licznika: każda kategoria × każda strona × każde miasto.</summary>
    private static int EstimateLegacy(string categories, int pages, int cityCount) =>
        categories.Split('|', StringSplitOptions.RemoveEmptyEntries).Length * pages * Math.Max(1, cityCount);
}
