namespace LeadFinder.Models;

/// <summary>Biznes zwrócony przez Google Places API (tylko pola z FieldMask).</summary>
/// <param name="BusinessStatus">
/// Status z Google: "OPERATIONAL", "CLOSED_TEMPORARILY" albo "CLOSED_PERMANENTLY"; null, gdy Google go nie podał.
/// </param>
public sealed record Place(
    string Id,
    string Name,
    string? Address,
    string? Phone,
    string? WebsiteUri,
    double? Rating,
    int? UserRatingCount,
    string? BusinessStatus = null);
