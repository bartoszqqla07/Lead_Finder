namespace LeadFinder.Models;

public enum SearchStage
{
    /// <summary>Zapytania do Google Places.</summary>
    Searching,

    /// <summary>Sprawdzanie stron WWW.</summary>
    CheckingWebsites,

    /// <summary>Skan wielu miast: przejście do kolejnego miasta.</summary>
    Region,
}

/// <summary>Zdarzenie postępu wyszukiwania (do logu w konsoli albo paska postępu w UI).</summary>
/// <param name="Stage">Etap wyszukiwania.</param>
/// <param name="Message">Opis zdarzenia dla człowieka.</param>
/// <param name="Current">Numer bieżącego kroku w etapie (od 1); 0 = komunikat ogólny etapu.</param>
/// <param name="Total">Liczba kroków w etapie.</param>
public sealed record SearchProgress(SearchStage Stage, string Message, int Current = 0, int Total = 0);
