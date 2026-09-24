using System.Text.Json;
using LeadFinder.Common;

namespace LeadFinder.Config;

/// <summary>Województwo z listą miast do skanu regionalnego (pierwsze miasto = wojewódzkie).</summary>
public sealed record Region(string Id, string Name, IReadOnlyList<string> Cities);

/// <summary>Województwa i miasta wczytane z Config/regions.json.</summary>
public sealed class RegionCatalog
{
    private const string RelativePath = "Config/regions.json";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private RegionCatalog(IReadOnlyList<Region> regions) => Regions = regions;

    public IReadOnlyList<Region> Regions { get; }

    /// <summary>Ścieżka jak w <see cref="CategoryCatalog.ResolveDefaultPath"/>: katalog roboczy, potem obok aplikacji.</summary>
    public static string ResolveDefaultPath()
    {
        var fromWorkingDirectory = Path.Combine(Directory.GetCurrentDirectory(), RelativePath);
        return File.Exists(fromWorkingDirectory)
            ? fromWorkingDirectory
            : Path.Combine(AppContext.BaseDirectory, RelativePath);
    }

    /// <summary>Wczytuje listę województw; brak pliku = pusta lista (skany regionalne po prostu niedostępne).</summary>
    /// <exception cref="ConfigurationException">Niepoprawny JSON.</exception>
    public static async Task<RegionCatalog> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path))
            return new RegionCatalog([]);

        try
        {
            await using var stream = File.OpenRead(path);
            var file = await JsonSerializer.DeserializeAsync<RegionsFile>(stream, JsonOptions, cancellationToken);
            var regions = (file?.Regions ?? [])
                .Where(r => !string.IsNullOrWhiteSpace(r.Id) && r.Cities is { Count: > 0 })
                .Select(r => new Region(
                    r.Id!.Trim(),
                    string.IsNullOrWhiteSpace(r.Name) ? r.Id!.Trim() : r.Name.Trim(),
                    r.Cities!.Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c.Trim()).Distinct().ToList()))
                .ToList();
            return new RegionCatalog(regions);
        }
        catch (JsonException ex)
        {
            throw new ConfigurationException($"Niepoprawny JSON w {path} (linia {ex.LineNumber + 1}): {ex.Message}", ex);
        }
    }

    private sealed class RegionsFile
    {
        public List<RegionEntry>? Regions { get; set; }
    }

    private sealed class RegionEntry
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public List<string>? Cities { get; set; }
    }
}
