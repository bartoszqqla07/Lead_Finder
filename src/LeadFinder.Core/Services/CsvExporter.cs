using System.Globalization;
using System.Text;
using LeadFinder.Common;
using LeadFinder.Models;

namespace LeadFinder.Services;

/// <summary>
/// Eksport leadów do CSV dopasowanego do polskiego Excela.
/// </summary>
/// <remarks>
/// Zapis ręczny zamiast CsvHelpera: jedyna nietrywialna część to escapowanie pól (kilkanaście linii),
/// a pakiet zwróciłby się dopiero przy czytaniu CSV albo mapowaniu wielu klas.
///
/// Trzy decyzje wynikają z zachowania Excela z polskimi ustawieniami regionalnymi:
///  - UTF-8 z BOM: bez BOM Excel czyta plik jako Windows-1250 i psuje polskie znaki,
///  - separator ";": przy polskich ustawieniach Excel po dwukliku rozdziela kolumny średnikiem
///    (przecinek jest separatorem dziesiętnym), plik z "," wylądowałby w jednej kolumnie,
///  - ocena z przecinkiem ("4,7"): "4.7" Excel zamieniłby na datę (4 lipca).
/// </remarks>
public sealed class CsvExporter
{
    public const char Delimiter = ';';

    private static readonly string[] Header =
    [
        "Nazwa", "Kategoria", "Adres", "Telefon", "Strona", "Status", "Szansa",
        "Technologia", "Ocena", "LiczbaOpinii", "SzkicWiadomosci",
    ];

    private static readonly CultureInfo PolishCulture = CultureInfo.GetCultureInfo("pl-PL");

    private readonly MessageDrafter _messageDrafter;

    /// <param name="messageDrafter">Generuje treść kolumny SzkicWiadomosci.</param>
    public CsvExporter(MessageDrafter messageDrafter) => _messageDrafter = messageDrafter;

    /// <summary>Nazwa pliku w formacie <c>leady-{miasto}-{timestamp}.csv</c>.</summary>
    public static string CreateFileName(string city) =>
        $"leady-{TextNormalizer.ToSlug(city)}-{DateTime.Now:yyyyMMdd-HHmmss}.csv";

    /// <summary>
    /// Sortuje leady: najpierw gorące (brak strony / strona nie działa), potem WordPress, na końcu reszta.
    /// W obrębie grupy wyżej trafiają leady z większą szansą (<see cref="LeadScorer"/>).
    /// </summary>
    public static IReadOnlyList<Lead> SortByPriority(IEnumerable<Lead> leads) =>
        leads
            .OrderBy(lead => lead.Status.SortPriority())
            .ThenByDescending(lead => LeadScorer.Score(lead).Value)
            .ThenBy(lead => lead.Place.Name, StringComparer.Create(PolishCulture, ignoreCase: true))
            .ToList();

    /// <summary>Buduje treść CSV (bez BOM) w kolejności z <see cref="SortByPriority"/>.</summary>
    public string BuildCsv(IEnumerable<Lead> leads)
    {
        var builder = new StringBuilder();
        AppendRow(builder, Header);

        foreach (var lead in SortByPriority(leads))
        {
            var place = lead.Place;
            AppendRow(builder,
            [
                place.Name,
                lead.Category.Query,
                place.Address ?? string.Empty,
                place.Phone ?? string.Empty,
                place.WebsiteUri ?? string.Empty,
                lead.Status.ToLabel(),
                LeadScorer.Score(lead).Value.ToString(CultureInfo.InvariantCulture),
                lead.WebsiteCheck?.Note ?? "—",
                place.Rating?.ToString("0.0", PolishCulture) ?? string.Empty,
                place.UserRatingCount?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                _messageDrafter.Draft(lead),
            ]);
        }

        return builder.ToString();
    }

    /// <summary>Treść CSV jako bajty UTF-8 z BOM – do zapisu albo wysłania jako plik do pobrania.</summary>
    public byte[] BuildCsvBytes(IEnumerable<Lead> leads)
    {
        var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
        return [.. encoding.GetPreamble(), .. encoding.GetBytes(BuildCsv(leads))];
    }

    /// <summary>
    /// Zapisuje leady do <c>{outputDirectory}/leady-{miasto}-{timestamp}.csv</c> (UTF-8 z BOM).
    /// </summary>
    /// <returns>Pełna ścieżka zapisanego pliku.</returns>
    public async Task<string> ExportAsync(
        IEnumerable<Lead> leads, string city, string outputDirectory, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(outputDirectory);

        var path = Path.GetFullPath(Path.Combine(outputDirectory, CreateFileName(city)));
        await File.WriteAllBytesAsync(path, BuildCsvBytes(leads), cancellationToken);
        return path;
    }

    private static void AppendRow(StringBuilder builder, IReadOnlyList<string> fields)
    {
        for (var i = 0; i < fields.Count; i++)
        {
            if (i > 0)
                builder.Append(Delimiter);
            builder.Append(Escape(fields[i]));
        }

        builder.Append("\r\n"); // Excel oczekuje CRLF; wewnątrz pól zostają zwykłe \n (zawijanie w komórce)
    }

    private static string Escape(string value)
    {
        // Ochrona przed "CSV injection": Excel wykonałby pole zaczynające się od =, +, - lub @ jako formułę.
        // Nazwy firm z Google to dane z zewnątrz, więc lepiej dmuchać na zimne.
        if (value.Length > 0 && value[0] is '=' or '+' or '-' or '@' or '\t' or '\r')
            value = "'" + value;

        var needsQuoting = value.IndexOfAny([Delimiter, '"', '\n', '\r']) >= 0;
        return needsQuoting ? $"\"{value.Replace("\"", "\"\"")}\"" : value;
    }
}
