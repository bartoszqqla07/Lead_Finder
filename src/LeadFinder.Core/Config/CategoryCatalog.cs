using System.Text.Json;
using LeadFinder.Common;
using LeadFinder.Models;

namespace LeadFinder.Config;

/// <summary>Lista kategorii wczytana z categories.json i dopasowywanie nazw podanych w --categories.</summary>
public sealed class CategoryCatalog
{
    private const string RelativePath = "Config/categories.json";
    private const string DefaultTone = "default";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        ReadCommentHandling = JsonCommentHandling.Skip, // pozwala trzymać opis formatu w samym pliku
        AllowTrailingCommas = true,
    };

    private CategoryCatalog(IReadOnlyList<Category> categories) => Categories = categories;

    public IReadOnlyList<Category> Categories { get; }

    /// <summary>
    /// Ścieżka do pliku kategorii: najpierw katalog roboczy (przy <c>dotnet run</c> to katalog projektu,
    /// więc edycja działa bez przebudowy), potem kopia obok pliku .exe.
    /// </summary>
    public static string ResolveDefaultPath()
    {
        var fromWorkingDirectory = Path.Combine(Directory.GetCurrentDirectory(), RelativePath);
        return File.Exists(fromWorkingDirectory)
            ? fromWorkingDirectory
            : Path.Combine(AppContext.BaseDirectory, RelativePath);
    }

    /// <summary>Wczytuje i waliduje plik kategorii.</summary>
    /// <exception cref="ConfigurationException">Brak pliku, niepoprawny JSON lub niekompletne wpisy.</exception>
    public static async Task<CategoryCatalog> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path))
            throw new ConfigurationException($"Nie znaleziono pliku kategorii: {path}");

        CategoriesFile? file;
        try
        {
            await using var stream = File.OpenRead(path);
            file = await JsonSerializer.DeserializeAsync<CategoriesFile>(stream, JsonOptions, cancellationToken);
        }
        catch (JsonException ex)
        {
            throw new ConfigurationException(
                $"Niepoprawny JSON w {path} (linia {ex.LineNumber + 1}): {ex.Message}", ex);
        }

        var entries = file?.Categories ?? [];
        if (entries.Count == 0)
            throw new ConfigurationException($"Plik {path} nie zawiera żadnych kategorii.");

        var categories = new List<Category>(entries.Count);
        foreach (var (entry, index) in entries.Select((e, i) => (e, i)))
        {
            if (string.IsNullOrWhiteSpace(entry.Query))
                throw new ConfigurationException($"Kategoria nr {index + 1} w {path} nie ma pola \"query\".");

            categories.Add(new Category(
                Id: string.IsNullOrWhiteSpace(entry.Id) ? TextNormalizer.ToSlug(entry.Query) : entry.Id.Trim(),
                Query: entry.Query.Trim(),
                EnglishName: entry.EnglishName?.Trim() ?? string.Empty,
                Aliases: entry.Aliases ?? [],
                Tone: string.IsNullOrWhiteSpace(entry.Tone) ? DefaultTone : entry.Tone.Trim()));
        }

        var duplicateId = categories.GroupBy(c => c.Id).FirstOrDefault(g => g.Count() > 1)?.Key;
        if (duplicateId is not null)
            throw new ConfigurationException($"Zduplikowane id kategorii \"{duplicateId}\" w {path}.");

        return new CategoryCatalog(categories);
    }

    /// <summary>
    /// Zamienia nazwy z --categories na kategorie. Nazwa pasuje do id, frazy lub aliasu
    /// (bez względu na wielkość liter i polskie znaki). Nieznana nazwa jest używana wprost
    /// jako fraza wyszukiwania z neutralnym tonem wiadomości – o czym informuje <paramref name="onWarning"/>.
    /// </summary>
    /// <param name="requested">Nazwy podane przez użytkownika; null lub pusta lista = wszystkie kategorie.</param>
    /// <param name="onWarning">Wywoływane dla każdej nazwy spoza pliku konfiguracyjnego.</param>
    public IReadOnlyList<Category> Resolve(IReadOnlyList<string>? requested, Action<string>? onWarning = null)
    {
        if (requested is null || requested.Count == 0)
            return Categories;

        var resolved = new List<Category>();
        foreach (var name in requested)
        {
            var slug = TextNormalizer.ToSlug(name);
            var category = Categories.FirstOrDefault(c => Matches(c, slug));
            if (category is null)
            {
                onWarning?.Invoke(
                    $"Kategoria \"{name}\" nie występuje w categories.json – szukam jej wprost, z neutralnym tonem wiadomości.");
                category = new Category(slug, name.Trim(), string.Empty, [], DefaultTone);
            }

            if (!resolved.Contains(category))
                resolved.Add(category);
        }

        return resolved;
    }

    private static bool Matches(Category category, string slug) =>
        TextNormalizer.ToSlug(category.Id) == slug
        || TextNormalizer.ToSlug(category.Query) == slug
        || category.Aliases.Any(alias => TextNormalizer.ToSlug(alias) == slug);

    // Kształt pliku JSON – oddzielony od modelu domenowego, żeby brakujące pola dało się obsłużyć łagodnie.
    private sealed class CategoriesFile
    {
        public List<CategoryEntry>? Categories { get; set; }
    }

    private sealed class CategoryEntry
    {
        public string? Id { get; set; }
        public string? Query { get; set; }
        public string? EnglishName { get; set; }
        public List<string>? Aliases { get; set; }
        public string? Tone { get; set; }
    }
}
