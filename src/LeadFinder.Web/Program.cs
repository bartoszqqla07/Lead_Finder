using System.Diagnostics;
using System.Text;
using System.Text.Json.Serialization;
using LeadFinder.Common;
using LeadFinder.Config;
using LeadFinder.Services;
using LeadFinder.Web.Auth;
using LeadFinder.Web.Contracts;
using LeadFinder.Web.Data;
using LeadFinder.Web.Endpoints;
using LeadFinder.Web.Search;
using LeadFinder.Web.Settings;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;

Console.OutputEncoding = Encoding.UTF8;

// Azure App Service ustawia tę zmienną w każdej aplikacji – po niej rozpoznajemy tryb serwerowy.
var isAzure = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME"));

try
{
    var isDemo = args.Contains("--demo");

    // Wersja opublikowana (dist\ albo serwer) ma wwwroot obok .dll – wtedy to jest content root, niezależnie
    // od katalogu, z którego uruchomiono program. Przy `dotnet run` zostaje katalog projektu.
    var publishedRoot = Directory.Exists(Path.Combine(AppContext.BaseDirectory, "wwwroot")) ? AppContext.BaseDirectory : null;
    var builder = WebApplication.CreateBuilder(new WebApplicationOptions
    {
        Args = args.Where(a => a != "--demo").ToArray(),
        ContentRootPath = publishedRoot,
    });

    await EnvFileLoader.LoadAsync(Path.Combine(builder.Environment.ContentRootPath, ".env"));

    if (isAzure)
        ConfigureForAzure(builder);
    else if (string.IsNullOrEmpty(builder.Configuration["urls"]))
        builder.WebHost.UseUrls("http://localhost:5178"); // lokalnie: tylko ten komputer

    var dataDirectory = ResolveDataDirectory(builder.Configuration, isAzure, isDemo);
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
    builder.Services.AddLeadFinderAuth(builder.Configuration, dataDirectory);
    builder.Services.ConfigureHttpJsonOptions(options =>
        options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

    var app = builder.Build();

    await InitializeDatabaseAsync(app.Services, isDemo, isAzure);

    if (isAzure)
        app.UseForwardedHeaders(); // prawdziwe IP klienta (limit prób logowania) i schemat https zza proxy Azure

    app.UseExceptionHandler();
    app.UseDefaultFiles();
    app.UseStaticFiles(); // powłoka aplikacji (HTML/JS/CSS) jest publiczna, dane z /api – już nie
    app.UseAuthentication();
    app.UseAuthorization();
    app.UseRateLimiter();

    app.MapAuthEndpoints();
    app.MapLeadEndpoints();
    app.MapSearchEndpoints();
    app.MapSettingsEndpoints();
    app.MapFallback("/api/{**path}", () => Results.NotFound());
    app.MapFallbackToFile("index.html").AllowAnonymous(); // SPA: każdy inny adres obsługuje React

    if (!isAzure)
    {
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
    }

    await app.RunAsync();
    return 0;
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    // Np. zajęty port, uszkodzony categories.json albo brak hasła na serwerze.
    Console.Error.WriteLine();
    Console.Error.WriteLine($"Nie udało się uruchomić LeadFindera: {ex.Message}");
    if (ex is IOException && ex.Message.Contains("address already in use", StringComparison.OrdinalIgnoreCase))
        Console.Error.WriteLine("Port jest zajęty – może LeadFinder już działa w innym oknie?");

    // Przy uruchomieniu dwuklikiem okno zamknęłoby się od razu, więc czekamy na Enter (nie na serwerze).
    if (!isAzure && Environment.UserInteractive && !Console.IsInputRedirected)
    {
        Console.Error.WriteLine("Naciśnij Enter, aby zamknąć.");
        Console.ReadLine();
    }

    return 1;
}

// Ustawienia specyficzne dla Azure App Service.
static void ConfigureForAzure(WebApplicationBuilder builder)
{
    // Aplikacja w internecie bez hasła oznaczałaby, że każdy może zużywać klucz Google i czytać leady.
    if (!AuthSetup.IsEnabled(builder.Configuration))
    {
        throw new ConfigurationException(
            "Na serwerze wymagane jest hasło. Ustaw w Azure (Configuration → Environment variables) zmienną " +
            "LeadFinder__Password.");
    }

    // Lokalnie AllowedHosts to tylko localhost (ochrona przed DNS rebinding); na serwerze – jego domena.
    var hostName = Environment.GetEnvironmentVariable("WEBSITE_HOSTNAME");
    if (!string.IsNullOrEmpty(hostName) && builder.Configuration["AllowedHosts"] == "localhost;127.0.0.1")
        builder.Configuration["AllowedHosts"] = hostName;

    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        // Proxy Azure nie ma stałego adresu, a aplikacja i tak nie jest osiągalna inaczej niż przez nie.
        options.KnownNetworks.Clear();
        options.KnownProxies.Clear();
    });
}

// Gdzie trzymać bazę: na Azure w trwałym %HOME% (przetrwa restart i wdrożenie), lokalnie w profilu użytkownika.
static string ResolveDataDirectory(IConfiguration configuration, bool isAzure, bool isDemo)
{
    var directory = configuration["LeadFinder:DataDirectory"] is { Length: > 0 } configured
        ? Path.GetFullPath(configured)
        : isAzure
            ? Path.Combine(Environment.GetEnvironmentVariable("HOME") ?? AppContext.BaseDirectory, "data", "LeadFinder")
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LeadFinder");

    if (isDemo)
        directory = Path.Combine(directory, "demo");

    Directory.CreateDirectory(directory);
    return directory;
}

static async Task InitializeDatabaseAsync(IServiceProvider services, bool isDemo, bool isAzure)
{
    using var scope = services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<LeadFinderDbContext>();
    await db.Database.MigrateAsync();

    // EF Core tworzy bazy SQLite w trybie WAL, który wymaga pamięci współdzielonej i nie działa niezawodnie
    // na dyskach sieciowych – a %HOME% w Azure App Service to właśnie udział sieciowy. Tryb DELETE jest tam bezpieczny.
    if (isAzure)
        await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=DELETE;");

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
