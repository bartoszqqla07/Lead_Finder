namespace LeadFinder.Config;

/// <summary>
/// Minimalny loader pliku .env: <c>KEY=value</c>, opcjonalne cudzysłowy, komentarze <c>#</c>,
/// opcjonalny prefiks <c>export</c>. Zmienne już ustawione w środowisku mają pierwszeństwo.
/// </summary>
public static class EnvFileLoader
{
    /// <summary>Wczytuje zmienne z pliku .env; brak pliku nie jest błędem.</summary>
    /// <returns>Liczba ustawionych zmiennych.</returns>
    public static async Task<int> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path))
            return 0;

        var loaded = 0;
        // ReadAllLinesAsync sam usuwa BOM, który dodaje np. Notatnik.
        foreach (var rawLine in await File.ReadAllLinesAsync(path, cancellationToken))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;

            if (line.StartsWith("export ", StringComparison.Ordinal))
                line = line["export ".Length..].TrimStart();

            var separator = line.IndexOf('=');
            if (separator <= 0)
                continue;

            var key = line[..separator].Trim();
            var value = ParseValue(line[(separator + 1)..].Trim());

            if (Environment.GetEnvironmentVariable(key) is null)
            {
                Environment.SetEnvironmentVariable(key, value);
                loaded++;
            }
        }

        return loaded;
    }

    private static string ParseValue(string value)
    {
        var isQuoted = value.Length >= 2
            && (value[0] == '"' || value[0] == '\'')
            && value[^1] == value[0];
        if (isQuoted)
            return value[1..^1];

        // Komentarz na końcu linii tylko po białym znaku, żeby "abc#def" zostało nietknięte.
        var commentStart = value.IndexOf(" #", StringComparison.Ordinal);
        return commentStart >= 0 ? value[..commentStart].TrimEnd() : value;
    }
}
