namespace LeadFinder.Models;

public enum LeadStatus
{
    NoWebsite,
    WebsiteDown,
    WordPress,
    HasWebsite,
}

public static class LeadStatusExtensions
{
    /// <summary>Etykieta statusu do CSV.</summary>
    public static string ToLabel(this LeadStatus status) => status switch
    {
        LeadStatus.NoWebsite => "BRAK STRONY — gorący lead",
        LeadStatus.WebsiteDown => "STRONA NIE DZIAŁA — gorący lead",
        LeadStatus.WordPress => "WordPress — możliwy target",
        LeadStatus.HasWebsite => "ma stronę — niski priorytet",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };

    /// <summary>Kolejność w CSV: 0 = gorące leady (oba statusy razem), 1 = WordPress, 2 = reszta.</summary>
    public static int SortPriority(this LeadStatus status) => status switch
    {
        LeadStatus.NoWebsite or LeadStatus.WebsiteDown => 0,
        LeadStatus.WordPress => 1,
        _ => 2,
    };
}
