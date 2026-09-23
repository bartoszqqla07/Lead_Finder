namespace LeadFinder.Models;

/// <summary>Sklasyfikowany biznes.</summary>
/// <param name="Place">Dane z Google Places.</param>
/// <param name="Category">Kategoria, w której biznes znaleziono jako pierwszy (decyduje o tonie wiadomości).</param>
/// <param name="City">Miasto wyszukiwania.</param>
/// <param name="Status">Klasyfikacja leada.</param>
/// <param name="WebsiteCheck">Wynik sprawdzenia strony; null, gdy biznes nie podał strony.</param>
/// <remarks>
/// Szkic wiadomości nie jest częścią modelu: generuje go <see cref="Services.MessageDrafter"/> w chwili
/// użycia, dzięki czemu zmiana podpisu czy szablonu obejmuje też leady zapisane wcześniej.
/// </remarks>
public sealed record Lead(
    Place Place,
    Category Category,
    string City,
    LeadStatus Status,
    WebsiteCheckResult? WebsiteCheck);
