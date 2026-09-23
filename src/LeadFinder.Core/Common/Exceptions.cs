namespace LeadFinder.Common;

/// <summary>
/// Bazowy wyjątek aplikacji: jego <see cref="Exception.Message"/> jest przeznaczony dla użytkownika
/// i wypisywany bez stacktrace'u.
/// </summary>
public class LeadFinderException(string message, Exception? innerException = null)
    : Exception(message, innerException);

/// <summary>Błędne argumenty wywołania (exit code 2).</summary>
public sealed class CliException(string message) : LeadFinderException(message);

/// <summary>Brakująca lub niepoprawna konfiguracja (klucz API, categories.json).</summary>
public sealed class ConfigurationException(string message, Exception? innerException = null)
    : LeadFinderException(message, innerException);
