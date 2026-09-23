namespace LeadFinder.Models;

/// <summary>Biznes zwrócony przez Google Places API (tylko pola z FieldMask).</summary>
public sealed record Place(
    string Id,
    string Name,
    string? Address,
    string? Phone,
    string? WebsiteUri,
    double? Rating,
    int? UserRatingCount);
