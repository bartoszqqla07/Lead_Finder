using LeadFinder.Models;

namespace LeadFinder.Cli;

/// <summary>
/// Wypisuje postęp wyszukiwania do konsoli. Celowo nie używa <see cref="Progress{T}"/>, które
/// w aplikacji konsolowej wywołuje handler na puli wątków – linie logu mogłyby się pomieszać.
/// </summary>
public sealed class ConsoleProgress : IProgress<SearchProgress>
{
    public void Report(SearchProgress value)
    {
        var counter = value.Current > 0 ? $"[{value.Current}/{value.Total}] " : string.Empty;
        var indent = value.Current > 0 ? "  " : string.Empty;
        Console.WriteLine($"{indent}{counter}{value.Message}");
    }
}
