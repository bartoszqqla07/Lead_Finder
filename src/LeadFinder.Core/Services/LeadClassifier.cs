using LeadFinder.Models;

namespace LeadFinder.Services;

public static class LeadClassifier
{
    /// <summary>
    /// Klasyfikuje biznes na podstawie strony podanej w Google i wyniku jej sprawdzenia.
    /// Profil na platformie (Booksy, Facebook…) liczy się jak brak strony.
    /// </summary>
    /// <param name="place">Biznes z Google Places.</param>
    /// <param name="websiteCheck">Wynik sprawdzenia strony; null, gdy biznes nie podał strony.</param>
    public static LeadStatus Classify(Place place, WebsiteCheckResult? websiteCheck)
    {
        if (string.IsNullOrWhiteSpace(place.WebsiteUri) || websiteCheck?.ProfilePlatform is not null)
            return LeadStatus.NoWebsite;

        if (websiteCheck is null || !websiteCheck.Reachable)
            return LeadStatus.WebsiteDown;

        // Sam WordPress to nie problem – dopracowana strona na WordPressie to "ma stronę", nie kandydat do odświeżenia.
        return websiteCheck.IsWordPress && !websiteCheck.LooksModern ? LeadStatus.WordPress : LeadStatus.HasWebsite;
    }
}
