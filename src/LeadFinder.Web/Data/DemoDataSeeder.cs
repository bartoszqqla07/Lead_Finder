using System.Text.RegularExpressions;
using LeadFinder.Models;
using Microsoft.EntityFrameworkCore;

namespace LeadFinder.Web.Data;

/// <summary>
/// Fikcyjne dane do trybu demo (<c>--demo</c>): pokazanie aplikacji bez klucza API.
/// Tryb demo używa osobnego pliku bazy, więc nie miesza się z prawdziwymi leadami.
/// </summary>
public static class DemoDataSeeder
{
    private sealed record DemoLead(
        string Name, string Street, string CategoryId, string Query, string Tone, LeadStatus Status,
        string? Website, string? Note, double Rating, int Reviews, OutreachStage Stage = OutreachStage.New,
        string Notes = "", string? Platform = null, bool IsWordPress = false,
        bool? Mobile = null, int? Year = null, bool? Https = null);

    private static readonly DemoLead[] Leads =
    [
        new("Barber Brzytwa", "ul. Mariacka 12", "barber-shop", "barber shop", "barber", LeadStatus.NoWebsite, null, null, 4.9, 312),
        new("Salon Fryzjerski Anna", "ul. Stawowa 3", "salon-fryzjerski", "salon fryzjerski", "hair", LeadStatus.NoWebsite, null, null, 4.7, 88, OutreachStage.Contacted, "Wizyta osobista 12.09 – właścicielka zainteresowana, oddzwonić po 20-tym."),
        new("Nails by Ola", "ul. Warszawska 41", "salon-paznokci", "salon paznokci", "nails", LeadStatus.NoWebsite, "https://booksy.com/pl-pl/00000_nails-by-ola", "tylko profil Booksy, brak własnej strony", 4.8, 156, Platform: "Booksy"),
        new("Studio Urody Bella", "ul. Chorzowska 150", "studio-urody", "studio urody", "beauty", LeadStatus.WebsiteDown, "http://studio-bella.example", "domena nie istnieje lub nie ma rekordów DNS", 4.6, 47),
        new("Gentlemen's Cut", "ul. 3 Maja 7", "barber-shop", "barber shop", "barber", LeadStatus.WebsiteDown, "https://gentlemens-cut.example", "brak odpowiedzi w 8 s (timeout)", 4.5, 203, OutreachStage.Replied, "Odpowiedź na IG – chcą wycenę strony z rezerwacją."),
        new("Oaza Day Spa", "ul. Sokolska 66", "salon-spa", "salon spa", "spa", LeadStatus.WordPress, "https://oaza-spa.example", "HTTP 200 · WordPress 5.8.1 (wp-content, wp-json) · brak wersji na telefon · stopka © 2018", 4.6, 134, IsWordPress: true, Mobile: false, Year: 2018, Https: true),
        new("Ink Point Tattoo", "ul. Mickiewicza 20", "salon-tatuazu", "salon tatuażu", "tattoo", LeadStatus.WordPress, "https://inkpoint.example", "HTTP 200 · WordPress (wp-content, wp-includes)", 4.9, 421, IsWordPress: true, Mobile: true, Https: true),
        new("Gabinet Kosmetologii Derma+", "ul. Francuska 34", "gabinet-kosmetologii", "gabinet kosmetologii", "cosmetology", LeadStatus.WordPress, "https://derma-plus.example", "HTTP 200 · WordPress 4.9.8 (wp-content, wp-json) · brak HTTPS · brak wersji na telefon · stopka © 2016", 4.4, 61, OutreachStage.Client, "Umowa podpisana, start 1.10.", IsWordPress: true, Mobile: false, Year: 2016, Https: false),
        new("Hair Loft", "ul. Dworcowa 9", "salon-fryzjerski", "salon fryzjerski", "hair", LeadStatus.HasWebsite, "https://hairloft.example", "HTTP 200 · Wix", 4.8, 290, Mobile: true, Https: true),
        new("Kosmetyka Natura", "ul. Kościuszki 88", "salon-kosmetyczny", "salon kosmetyczny", "beauty", LeadStatus.HasWebsite, "https://kosmetyka-natura.example", "HTTP 200 · nie rozpoznano CMS · stopka © 2019", 4.3, 35, OutreachStage.Rejected, "Mają agencję, nie są zainteresowani.", Mobile: true, Year: 2019, Https: true),
        new("Barbershop Kowal", "ul. Wojewódzka 5", "barber-shop", "barber shop", "barber", LeadStatus.NoWebsite, "https://www.instagram.com/barbershop.kowal", "tylko profil Instagram, brak własnej strony", 4.7, 97, Platform: "Instagram"),
        new("Manicure Studio Lila", "ul. Piastowska 2", "salon-paznokci", "salon paznokci", "nails", LeadStatus.NoWebsite, null, null, 4.2, 12),
    ];

    /// <summary>Wypełnia pustą bazę danymi demo.</summary>
    public static async Task SeedAsync(LeadFinderDbContext db, CancellationToken cancellationToken = default)
    {
        if (await db.Leads.AnyAsync(cancellationToken))
            return;

        var now = DateTime.UtcNow;
        var olderRun = new SearchRunEntity
        {
            City = "Katowice", Categories = "salon fryzjerski|salon kosmetyczny|gabinet kosmetologii", Pages = 3,
            State = SearchRunState.Completed, StartedAt = now.AddDays(-9), FinishedAt = now.AddDays(-9).AddMinutes(4),
            FoundCount = 6, NewCount = 6,
        };
        var latestRun = new SearchRunEntity
        {
            City = "Katowice", Categories = "barber shop|salon paznokci|salon spa|studio urody|salon tatuażu", Pages = 3,
            State = SearchRunState.Completed, StartedAt = now.AddHours(-2), FinishedAt = now.AddHours(-2).AddMinutes(3),
            FoundCount = 9, NewCount = 6,
        };
        db.SearchRuns.AddRange(olderRun, latestRun);
        await db.SaveChangesAsync(cancellationToken);

        for (var i = 0; i < Leads.Length; i++)
        {
            var demo = Leads[i];
            var isOld = demo.Stage != OutreachStage.New || demo.CategoryId is "salon-fryzjerski" or "salon-kosmetyczny";
            var run = isOld ? olderRun : latestRun;

            db.Leads.Add(new LeadEntity
            {
                PlaceId = $"demo-place-{i + 1}",
                Name = demo.Name,
                Address = $"{demo.Street}, 40-000 Katowice",
                Phone = $"32 {100 + i * 7} {10 + i} {20 + i}",
                WebsiteUri = demo.Website,
                Rating = demo.Rating,
                UserRatingCount = demo.Reviews,
                City = "Katowice",
                CategoryId = demo.CategoryId,
                CategoryQuery = demo.Query,
                CategoryTone = demo.Tone,
                Status = demo.Status,
                WebsiteReachable = demo.Website is null ? null : demo.Status != LeadStatus.WebsiteDown,
                IsWordPress = demo.IsWordPress,
                CheckNote = demo.Note,
                ProfilePlatform = demo.Platform,
                // Jak przy prawdziwym sprawdzeniu: technologia z wersją, np. "WordPress 5.8.1" (z notatki demo).
                Technology = demo.Note is null ? null : Regex.Match(demo.Note, @"(WordPress(?: [0-9.]+)?|Wix)").Value is { Length: > 0 } tech ? tech : null,
                IsMobileFriendly = demo.Mobile,
                CopyrightYear = demo.Year,
                UsesHttps = demo.Https,
                BusinessStatus = "OPERATIONAL",
                Stage = demo.Stage,
                Notes = demo.Notes,
                StageChangedAt = demo.Stage == OutreachStage.New ? null : now.AddDays(-3),
                FirstSeenAt = run.StartedAt,
                LastSeenAt = latestRun.StartedAt,
                FirstSearchRunId = run.Id,
                LastSearchRunId = latestRun.Id,
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
