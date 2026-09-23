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

        return websiteCheck.IsWordPress ? LeadStatus.WordPress : LeadStatus.HasWebsite;
    }
}
