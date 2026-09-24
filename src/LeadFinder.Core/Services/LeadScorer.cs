using System.Globalization;
using LeadFinder.Common;
using LeadFinder.Models;

namespace LeadFinder.Services;

/// <summary>
/// Szacuje (0–100), jak duża jest szansa, że firma zechce nową stronę.
/// </summary>
/// <remarks>
/// To heurystyka, nie model statystyczny: suma punktów za jawne, sprawdzalne sygnały. Każdy składnik trafia
/// do wyniku z opisem, więc w UI widać, skąd wzięła się ocena. Wagi są w jednym miejscu – warto je
/// korygować, gdy okaże się, które firmy faktycznie odpowiadają.
///
/// Trzy grupy sygnałów:
///  1. Potrzeba – sytuacja ze stroną (brak, nie działa, przestarzała). Najwięcej punktów.
///  2. Możliwości – ruch w firmie (liczba opinii) i dbałość o wizerunek (ocena). Większy salon = większy budżet.
///  3. Branża – tam, gdzie klienci czytają opisy drogich usług, strona sprzedaje najbardziej.
/// </remarks>
public static class LeadScorer
{
    public const int HighThreshold = 65;
    public const int MediumThreshold = 40;

    private static readonly CultureInfo Polish = CultureInfo.GetCultureInfo("pl-PL");

    /// <summary>Ocenia lead na podstawie danych z Google i wyniku sprawdzenia strony.</summary>
    public static LeadScore Score(Lead lead)
    {
        var place = lead.Place;
        if (place.BusinessStatus == "CLOSED_PERMANENTLY")
            return new LeadScore(0, ScoreTier.Low, [new ScoreFactor("Firma zamknięta na stałe (według Google)", 0)]);

        var factors = new List<ScoreFactor>();
        AddNeedFactors(lead, factors);
        AddContextFactors(place, lead.Category, factors);

        var value = Math.Clamp(factors.Sum(f => f.Points), 0, 100);
        var ordered = factors.OrderByDescending(f => f.Points).ToList();
        return new LeadScore(value, TierOf(value), ordered);
    }

    /// <summary>Najwięcej punktów, jakie da się zdobyć za "potrzebę" (przestarzały WordPress ze wszystkimi sygnałami).</summary>
    private const int MaxNeedPoints = 20 + 10 + 15 + 8 + 7;

    /// <summary>
    /// Najwyższy wynik, jaki firma może dostać, zanim sprawdzimy jej stronę. Pozwala pominąć (powolne)
    /// sprawdzanie stron firm, które i tak nie przekroczą progu – np. przy skanie województwa z progiem 75+.
    /// Dla firm bez strony to po prostu ich wynik.
    /// </summary>
    public static int PotentialScore(Place place, Category category)
    {
        if (string.IsNullOrWhiteSpace(place.WebsiteUri))
            return Score(new Lead(place, category, string.Empty, LeadStatus.NoWebsite, null)).Value;
        if (place.BusinessStatus == "CLOSED_PERMANENTLY")
            return 0;

        var factors = new List<ScoreFactor>();
        AddContextFactors(place, category, factors);
        return Math.Clamp(MaxNeedPoints + factors.Sum(f => f.Points), 0, 100);
    }

    /// <summary>Wszystko poza stanem strony: ruch, ocena, branża, kontakt, status firmy.</summary>
    private static void AddContextFactors(Place place, Category category, List<ScoreFactor> factors)
    {
        AddActivityFactors(place, factors);
        AddCategoryFactor(category, factors);

        if (string.IsNullOrWhiteSpace(place.Phone))
            factors.Add(new("Brak telefonu w wizytówce – trudniej o kontakt", -5));
        if (place.BusinessStatus == "CLOSED_TEMPORARILY")
            factors.Add(new("Tymczasowo zamknięta (według Google)", -20));
    }

    public static ScoreTier TierOf(int value) => value switch
    {
        >= HighThreshold => ScoreTier.High,
        >= MediumThreshold => ScoreTier.Medium,
        _ => ScoreTier.Low,
    };

    /// <summary>Potrzeba: czy i jak bardzo brakuje im dobrej strony.</summary>
    private static void AddNeedFactors(Lead lead, List<ScoreFactor> factors)
    {
        var check = lead.WebsiteCheck;

        switch (lead.Status)
        {
            case LeadStatus.WebsiteDown:
                factors.Add(new("Strona nie działa – kiedyś za nią zapłacili, więc znają jej wartość", 45));
                break;
            case LeadStatus.NoWebsite when check?.ProfilePlatform == "Booksy":
                factors.Add(new("Tylko Booksy – rezerwują online, ale nie mają własnej strony", 42));
                break;
            case LeadStatus.NoWebsite when check?.ProfilePlatform is { } platform:
                factors.Add(new($"Tylko profil {platform} zamiast strony", 38));
                break;
            case LeadStatus.NoWebsite:
                factors.Add(new("Brak strony internetowej", 35));
                break;
            case LeadStatus.WordPress:
                factors.Add(new("Strona na WordPressie – kandydat do odświeżenia", 20));
                if (WordPressMajorVersion(check?.Technology) is < 6)
                    factors.Add(new($"Stara wersja WordPressa ({check!.Technology!["WordPress".Length..].Trim()})", 10));
                break;
            default:
                factors.Add(new("Ma działającą stronę – mniejsza potrzeba", 0));
                break;
        }

        // Sygnały przestarzałej strony – tylko gdy udało się ją pobrać (dla niedziałającej nic nie wiemy).
        if (check is not { Reachable: true, ProfilePlatform: null })
            return;

        if (check.IsMobileFriendly == false)
            factors.Add(new("Strona nie jest dostosowana do telefonów", 15));
        if (check.CopyrightYear is { } year && year <= DateTime.Now.Year - WebsiteChecker.OutdatedCopyrightYears)
            factors.Add(new($"Stopka z {year} r. – strona dawno nieaktualizowana", 8));
        if (check.UsesHttps == false)
            factors.Add(new("Brak HTTPS – przeglądarka pokazuje „Niezabezpieczona”", 7));
    }

    /// <summary>Możliwości: wielkość/ruch (liczba opinii) i dbałość o wizerunek (ocena).</summary>
    private static void AddActivityFactors(Place place, List<ScoreFactor> factors)
    {
        var reviews = place.UserRatingCount ?? 0;
        var reviewsText = $"{reviews} {PolishPlural.Reviews(reviews)}";
        factors.Add(reviews switch
        {
            >= 200 => new($"Duży ruch: {reviewsText}", 20),
            >= 80 => new($"Spory ruch: {reviewsText}", 16),
            >= 30 => new($"Stali klienci: {reviewsText}", 12),
            >= 10 => new($"Niewiele opinii ({reviews}) – mniejszy salon", 6),
            _ => new($"Bardzo mało opinii ({reviews}) – mała lub nowa firma", 0),
        });

        if (place.Rating is not { } rating)
            return;

        var ratingText = rating.ToString("0.0", Polish);
        if (rating >= 4.7 && reviews >= 10)
            factors.Add(new($"Świetna ocena ({ratingText}) – dbają o wizerunek", 8));
        else if (rating >= 4.3)
            factors.Add(new($"Dobra ocena ({ratingText})", 4));
        else if (rating < 4.0)
            factors.Add(new($"Niska ocena ({ratingText}) – mogą mieć ważniejsze problemy", -5));
    }

    /// <summary>Branża: jak bardzo strona sprzedaje w danym typie usług.</summary>
    private static void AddCategoryFactor(Category category, List<ScoreFactor> factors) =>
        factors.Add(category.Tone.ToLowerInvariant() switch
        {
            "spa" or "cosmetology" => new("Drogie usługi – opis zabiegów na stronie realnie sprzedaje", 8),
            "hair" or "beauty" => new("Klienci porównują salony w internecie przed wizytą", 5),
            "tattoo" => new("Studio tatuażu sprzedaje portfolio – strona to wizytówka", 5),
            "barber" => new("Barberzy często wystarczają sobie Booksy i Instagramem", 3),
            "nails" => new("Stylizacje sprzedaje galeria – często wystarcza im Instagram", 3),
            _ => new("Branża usługowa", 3),
        });

    /// <summary>"WordPress 5.8.1" → 5; null, gdy wersji nie znamy.</summary>
    private static int? WordPressMajorVersion(string? technology)
    {
        if (technology is null || !technology.StartsWith("WordPress", StringComparison.OrdinalIgnoreCase))
            return null;

        var version = technology["WordPress".Length..].Trim();
        var dot = version.IndexOf('.');
        return int.TryParse(dot > 0 ? version[..dot] : version, out var major) ? major : null;
    }
}
