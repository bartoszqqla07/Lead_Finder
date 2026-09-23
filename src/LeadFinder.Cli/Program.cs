using System.Text;
using LeadFinder.Cli;
using LeadFinder.Common;
using LeadFinder.Config;
using LeadFinder.Models;
using LeadFinder.Services;

const string ApiKeyVariable = "GOOGLE_PLACES_API_KEY";
const int ExitSuccess = 0;
const int ExitError = 1;
const int ExitUsageError = 2;
const int ExitCancelled = 130;

Console.OutputEncoding = Encoding.UTF8; // polskie znaki w konsoli Windows

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true; // zamiast natychmiastowego zabicia procesu: anuluj i pozwól posprzątać
    cts.Cancel();
};

try
{
    var options = CommandLineParser.Parse(args);
    if (options.ShowHelp)
    {
        Console.WriteLine(CommandLineParser.HelpText);
        return ExitSuccess;
    }

    var catalog = await CategoryCatalog.LoadAsync(CategoryCatalog.ResolveDefaultPath(), cts.Token);
    if (options.ListCategories)
    {
        foreach (var c in catalog.Categories)
            Console.WriteLine($"{c.Id,-22} \"{c.Query}\" ({c.EnglishName}) · aliasy: {string.Join(", ", c.Aliases)}");
        return ExitSuccess;
    }

    var categories = catalog.Resolve(options.Categories, warning => Console.WriteLine($"Uwaga: {warning}"));

    await EnvFileLoader.LoadAsync(Path.Combine(Directory.GetCurrentDirectory(), ".env"), cts.Token);
    var apiKey = Environment.GetEnvironmentVariable(ApiKeyVariable);
    if (string.IsNullOrWhiteSpace(apiKey))
    {
        throw new ConfigurationException(
            $"Brak klucza API. Ustaw zmienną {ApiKeyVariable} albo dodaj ją do pliku .env w katalogu roboczym " +
            "(wzór: .env.example). Jak zdobyć klucz – zobacz README.md.");
    }

    // Kompozycja zależności: zwykłe przekazanie przez konstruktory, bez kontenera DI.
    using var placesHttpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    using var websiteHttpClient = WebsiteChecker.CreateHttpClient();

    var pipeline = new LeadSearchPipeline(
        new PlacesApiClient(placesHttpClient, apiKey),
        new WebsiteChecker(websiteHttpClient));
    var csvExporter = new CsvExporter(new MessageDrafter());

    Console.WriteLine($"Szukam w: {options.City} · kategorie: {categories.Count} · maks. {options.Pages} str. na kategorię");
    var leads = await pipeline.RunAsync(
        new LeadSearchRequest(options.City!, categories, options.Pages),
        new ConsoleProgress(),
        cts.Token);

    var csvPath = await csvExporter.ExportAsync(leads, options.City!, options.OutputDirectory, cts.Token);
    PrintSummary(leads, csvPath);
    return ExitSuccess;
}
catch (CliException ex)
{
    Console.Error.WriteLine($"Błąd: {ex.Message}");
    Console.Error.WriteLine("Użyj --help, żeby zobaczyć dostępne opcje.");
    return ExitUsageError;
}
catch (LeadFinderException ex)
{
    Console.Error.WriteLine();
    Console.Error.WriteLine($"Błąd: {ex.Message}");
    return ExitError;
}
catch (OperationCanceledException) when (cts.IsCancellationRequested)
{
    Console.Error.WriteLine();
    Console.Error.WriteLine("Przerwano (Ctrl+C). Plik CSV nie został zapisany.");
    return ExitCancelled;
}
catch (Exception ex)
{
    // Nieprzewidziany błąd: krótki komunikat, pełny stacktrace tylko na życzenie.
    Console.Error.WriteLine();
    Console.Error.WriteLine($"Nieoczekiwany błąd: {ex.Message}");
    if (Environment.GetEnvironmentVariable("LEADFINDER_DEBUG") == "1")
        Console.Error.WriteLine(ex);
    else
        Console.Error.WriteLine("Uruchom z LEADFINDER_DEBUG=1, żeby zobaczyć szczegóły.");
    return ExitError;
}

static void PrintSummary(IReadOnlyList<Lead> leads, string csvPath)
{
    Console.WriteLine();
    Console.WriteLine("Podsumowanie:");
    foreach (var group in leads.GroupBy(l => l.Status).OrderBy(g => g.Key.SortPriority()))
        Console.WriteLine($"  {group.Key.ToLabel(),-34} {group.Count(),4}");

    Console.WriteLine($"Zapisano {leads.Count} leadów do: {csvPath}");
    Console.WriteLine("Szkice wiadomości są tylko do ręcznego przejrzenia – przeczytaj README (sekcja o przepisach) przed kontaktem.");
}
