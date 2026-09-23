using LeadFinder.Common;

namespace LeadFinder.Cli;

/// <summary>Sparsowane argumenty wywołania.</summary>
public sealed record CliOptions(
    string? City,
    IReadOnlyList<string>? Categories,
    int Pages,
    string OutputDirectory,
    bool ShowHelp,
    bool ListCategories);

/// <summary>
/// Ręczny parser argv. Przy pięciu opcjach to kilkadziesiąt linii kodu, a pakiet
/// System.CommandLine byłby jedyną zewnętrzną zależnością projektu.
/// Obsługuje formy "--city Katowice" i "--city=Katowice".
/// </summary>
public static class CommandLineParser
{
    public const int DefaultPages = 3;
    public const int MaxPages = 10;
    public const string DefaultOutputDirectory = "output";

    public const string HelpText = """
        LeadFinder – wyszukiwarka leadów wśród lokalnych biznesów usługowych (Google Places API).

        Użycie:
          dotnet run -- --city <miasto> [opcje]

        Opcje:
          -c, --city <miasto>          Miasto do przeszukania (wymagane), np. "Katowice"
          -k, --categories <lista>     Kategorie po przecinku, np. "fryzjer,barber" (domyślnie wszystkie)
          -p, --pages <liczba>         Strony wyników na kategorię, 1 strona ≈ 20 wyników (domyślnie 3)
          -o, --output <katalog>       Katalog na plik CSV (domyślnie "output")
              --list-categories        Pokaż kategorie z Config/categories.json i zakończ
          -h, --help                   Pokaż tę pomoc

        Klucz API: zmienna GOOGLE_PLACES_API_KEY albo plik .env w katalogu roboczym.

        Przykład:
          dotnet run -- --city "Katowice" --categories "fryzjer,barber" --pages 2
        """;

    /// <summary>Parsuje argumenty wywołania.</summary>
    /// <exception cref="CliException">Nieznana opcja, brak wartości lub niepoprawna wartość.</exception>
    public static CliOptions Parse(IReadOnlyList<string> args)
    {
        string? city = null;
        List<string>? categories = null;
        var pages = DefaultPages;
        var output = DefaultOutputDirectory;
        var showHelp = false;
        var listCategories = false;

        for (var i = 0; i < args.Count; i++)
        {
            var (name, inlineValue) = SplitInlineValue(args[i]);

            switch (name)
            {
                case "-h" or "--help" or "/?":
                    showHelp = true;
                    break;
                case "--list-categories":
                    listCategories = true;
                    break;
                case "-c" or "--city":
                    city = RequireValue(name, inlineValue, args, ref i);
                    break;
                case "-k" or "--categories":
                    categories = RequireValue(name, inlineValue, args, ref i)
                        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .ToList();
                    break;
                case "-p" or "--pages":
                    pages = ParsePages(RequireValue(name, inlineValue, args, ref i));
                    break;
                case "-o" or "--output":
                    output = RequireValue(name, inlineValue, args, ref i);
                    break;
                default:
                    throw new CliException($"Nieznana opcja: {args[i]}");
            }
        }

        if (!showHelp && !listCategories && string.IsNullOrWhiteSpace(city))
            throw new CliException("Podaj miasto, np. --city \"Katowice\".");

        return new CliOptions(city?.Trim(), categories, pages, output, showHelp, listCategories);
    }

    private static (string Name, string? InlineValue) SplitInlineValue(string arg)
    {
        var separator = arg.IndexOf('=');
        return arg.StartsWith("--", StringComparison.Ordinal) && separator > 0
            ? (arg[..separator], arg[(separator + 1)..])
            : (arg, null);
    }

    private static string RequireValue(string name, string? inlineValue, IReadOnlyList<string> args, ref int index)
    {
        if (inlineValue is not null)
            return inlineValue;

        if (index + 1 >= args.Count || args[index + 1].StartsWith('-'))
            throw new CliException($"Opcja {name} wymaga wartości.");

        return args[++index];
    }

    private static int ParsePages(string value) =>
        int.TryParse(value, out var pages) && pages is >= 1 and <= MaxPages
            ? pages
            : throw new CliException($"--pages musi być liczbą od 1 do {MaxPages} (podano \"{value}\").");
}
