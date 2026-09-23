using System.Diagnostics;
using System.Text;
using System.Text.Json.Serialization;
using LeadFinder.Config;
using LeadFinder.Services;
using LeadFinder.Web.Contracts;
using LeadFinder.Web.Data;
using LeadFinder.Web.Endpoints;
using LeadFinder.Web.Search;
using LeadFinder.Web.Settings;
using Microsoft.EntityFrameworkCore;

Console.OutputEncoding = Encoding.UTF8;

try
{
    var isDemo = args.Contains("--demo");

    // Wersja opublikowana (dist\) ma wwwroot obok .exe – wtedy to jest content root, niezależnie od katalogu,
    // z którego uruchomiono program (np. skrót na pulpicie). Przy `dotnet run` zostaje katalog projektu.
    var publishedRoot = Directory.Exists(Path.Combine(AppContext.BaseDirectory, "wwwroot")) ? AppContext.BaseDirectory : null;
    var builder = WebApplication.CreateBuilder(new WebApplicationOptions
    {
        Args = args.Where(a => a != "--demo").ToArray(),
        ContentRootPath = publishedRoot,
    });

    await EnvFileLoader.LoadAsync(Path.Combine(builder.Environment.ContentRootPath, ".env"));

    // Dane w profilu użytkownika, a nie obok .exe: aplikacja działa też z katalogu tylko do odczytu
    // i nie gubi bazy przy aktualizacji. Tryb demo ma własną bazę.
    var dataDirectory = builder.Configuration["LeadFinder:DataDirectory"] is { Length: > 0 } configured
        ? Path.GetFullPath(configured)
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LeadFinder");
    if (isDemo)
        dataDirectory = Path.Combine(dataDirectory, "demo");
    Directory.CreateDirectory(dataDirectory);
    var paths = new AppPaths(dataDirectory);

    var catalog = await CategoryCatalog.LoadAsync(CategoryCatalog.ResolveDefaultPath());

    builder.Services.AddSingleton(paths);
    builder.Services.AddSingleton(catalog);
    builder.Services.AddDbContext<LeadFinderDbContext>(options => options.UseSqlite($"Data Source={paths.DatabasePath}"));
    builder.Services.AddScoped<SettingsService>();
    builder.Services.AddSingleton(_ => new WebsiteChecker(WebsiteChecker.CreateHttpClient()));
    builder.Services.AddSingleton<SearchJobRunner>();
    builder.Services.AddHttpClient();
    builder.Services.AddProblemDetails();
    builder.Services.ConfigureHttpJsonOptions(options =>
        options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

    var app = builder.Build();

    await InitializeDatabaseAsync(app.Services, isDemo);

    app.UseExceptionHandler();
    app.UseDefaultFiles();
    app.UseStaticFiles();

    app.MapLeadEndpoints();
    app.MapSearchEndpoints();
    app.MapSettingsEndpoints();
    app.MapFallback("/api/{**path}", () => Results.NotFound());
    app.MapFallbackToFile("index.html"); // SPA: każdy inny adres obsługuje React

    app.Lifetime.ApplicationStarted.Register(() =>
    {
        var url = app.Urls.FirstOrDefault() ?? "http://localhost:5178";
        Console.WriteLine();
        Console.WriteLine($"LeadFinder działa: {url}{(isDemo ? "  (TRYB DEMO – przykładowe dane)" : "")}");
        Console.WriteLine($"Dane: {paths.DatabasePath}");
        Console.WriteLine("Zamknij to okno (lub Ctrl+C), żeby wyłączyć aplikację.");

        if (app.Configuration.GetValue("LeadFinder:OpenBrowser", true))
            OpenBrowser(url);
    });

    await app.RunAsync();
    return 0;
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    // Np. zajęty port albo uszkodzony categories.json. Przy uruchomieniu dwuklikiem okno
    // zamknęłoby się od razu, więc czekamy na Enter.
    Console.Error.WriteLine();
    Console.Error.WriteLine($"Nie udało się uruchomić LeadFindera: {ex.Message}");
    if (ex is IOException && ex.Message.Contains("address already in use", StringComparison.OrdinalIgnoreCase))
        Console.Error.WriteLine("Port jest zajęty – może LeadFinder już działa w innym oknie?");
    if (!Console.IsInputRedirected)
    {
        Console.Error.WriteLine("Naciśnij Enter, aby zamknąć.");
        Console.ReadLine();
    }

    return 1;
}

static async Task InitializeDatabaseAsync(IServiceProvider services, bool isDemo)
{
    using var scope = services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<LeadFinderDbContext>();
    await db.Database.MigrateAsync();

    // Wyszukiwania przerwane zamknięciem aplikacji zostałyby w historii jako "w toku".
    await db.SearchRuns
        .Where(r => r.State == SearchRunState.Running)
        .ExecuteUpdateAsync(s => s
            .SetProperty(r => r.State, SearchRunState.Failed)
            .SetProperty(r => r.Error, "Przerwane zamknięciem aplikacji."));

    if (isDemo)
        await DemoDataSeeder.SeedAsync(db);
}

static void OpenBrowser(string url)
{
    try
    {
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }
    catch (Exception)
    {
        // Brak domyślnej przeglądarki to nie powód, żeby zatrzymać serwer – adres jest w konsoli.
    }
}
