using LeadFinder.Models;
using LeadFinder.Services;
using LeadFinder.Web.Contracts;

namespace LeadFinder.Web.Data;

/// <summary>Mapowanie między encjami bazy, modelem domenowym z Core i DTO dla API.</summary>
public static class LeadMapping
{
    /// <summary>Odtwarza model domenowy – potrzebny do szkicu wiadomości i eksportu CSV z Core.</summary>
    public static Lead ToDomain(this LeadEntity entity)
    {
        var place = new Place(
            entity.PlaceId, entity.Name, entity.Address, entity.Phone,
            entity.WebsiteUri, entity.Rating, entity.UserRatingCount, entity.BusinessStatus);

        var category = new Category(entity.CategoryId, entity.CategoryQuery, string.Empty, [], entity.CategoryTone);

        var check = entity.CheckNote is null
            ? null
            : new WebsiteCheckResult(
                entity.WebsiteReachable ?? false, entity.IsWordPress, entity.CheckNote,
                entity.Technology, entity.ProfilePlatform,
                entity.IsMobileFriendly, entity.CopyrightYear, entity.UsesHttps);

        return new Lead(place, category, entity.City, entity.Status, check);
    }

    /// <summary>Kopiuje dane z Google i wynik sprawdzenia strony; nie dotyka pól CRM.</summary>
    public static void ApplySearchResult(this LeadEntity entity, Lead lead)
    {
        var place = lead.Place;
        entity.Name = place.Name;
        entity.Address = place.Address;
        entity.Phone = place.Phone;
        entity.WebsiteUri = place.WebsiteUri;
        entity.Rating = place.Rating;
        entity.UserRatingCount = place.UserRatingCount;
        entity.BusinessStatus = place.BusinessStatus;

        var check = lead.WebsiteCheck;
        entity.Status = lead.Status;
        entity.WebsiteReachable = check?.Reachable;
        entity.IsWordPress = check?.IsWordPress ?? false;
        entity.CheckNote = check?.Note;
        entity.Technology = check?.Technology;
        entity.ProfilePlatform = check?.ProfilePlatform;
        entity.IsMobileFriendly = check?.IsMobileFriendly;
        entity.CopyrightYear = check?.CopyrightYear;
        entity.UsesHttps = check?.UsesHttps;
    }

    /// <summary>
    /// DTO dla API. Szkice i ocena szansy są liczone przy odczycie, a nie zapisywane – zmiana podpisu
    /// albo wag w <see cref="LeadScorer"/> od razu obejmuje wszystkie leady.
    /// </summary>
    public static LeadDto ToDto(this LeadEntity entity, MessageDrafter drafter)
    {
        var lead = entity.ToDomain();
        return new LeadDto(
            entity.Id,
            entity.PlaceId,
            entity.Name,
            entity.Address,
            entity.Phone,
            entity.WebsiteUri,
            entity.Rating,
            entity.UserRatingCount,
            entity.City,
            entity.CategoryId,
            entity.CategoryQuery,
            entity.Status,
            entity.Status.ToLabel(),
            entity.Status.SortPriority(),
            entity.CheckNote,
            entity.Technology,
            entity.ProfilePlatform,
            entity.Stage,
            entity.Notes,
            entity.StageChangedAt,
            entity.ConsentGivenAt,
            entity.NextActionDate,
            entity.FirstSeenAt,
            entity.LastSeenAt,
            entity.FirstSearchRunId,
            entity.LastSearchRunId,
            drafter.CreateDrafts(lead),
            LeadScorer.Score(lead),
            entity.BusinessStatus,
            GoogleMapsUrl(entity));
    }

    public static SearchRunDto ToDto(this SearchRunEntity run) => new(
        run.Id,
        run.City,
        run.Categories.Split('|', StringSplitOptions.RemoveEmptyEntries),
        run.Pages,
        run.State,
        run.StartedAt,
        run.FinishedAt,
        run.FoundCount,
        run.NewCount,
        run.Error);

    /// <summary>
    /// Link "Maps URLs" (https://developers.google.com/maps/documentation/urls/get-started) –
    /// otwiera wizytówkę firmy bez klucza API.
    /// </summary>
    private static string GoogleMapsUrl(LeadEntity entity)
    {
        var query = Uri.EscapeDataString($"{entity.Name} {entity.Address}".Trim());
        return $"https://www.google.com/maps/search/?api=1&query={query}&query_place_id={Uri.EscapeDataString(entity.PlaceId)}";
    }
}
