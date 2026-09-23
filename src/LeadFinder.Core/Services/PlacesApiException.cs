using System.Net;
using System.Text.Json;
using LeadFinder.Common;

namespace LeadFinder.Services;

/// <summary>Błąd komunikacji z Google Places API, z komunikatem zrozumiałym dla użytkownika.</summary>
public sealed class PlacesApiException(string message, Exception? innerException = null)
    : LeadFinderException(message, innerException)
{
    /// <summary>
    /// Tłumaczy odpowiedź błędu Google na czytelny komunikat. Format błędu Google:
    /// <c>{"error": {"code": 403, "message": "...", "status": "PERMISSION_DENIED", "details": [...]}}</c>
    /// </summary>
    public static PlacesApiException FromErrorResponse(HttpStatusCode httpStatus, string responseBody)
    {
        var (status, googleMessage) = TryParseGoogleError(responseBody);
        var details = string.IsNullOrWhiteSpace(googleMessage) ? string.Empty : $"\nSzczegóły od Google: {googleMessage}";

        // Zły klucz Google zgłasza jako 400 INVALID_ARGUMENT z powodem API_KEY_INVALID.
        if (responseBody.Contains("API_KEY_INVALID", StringComparison.Ordinal)
            || googleMessage?.Contains("API key not valid", StringComparison.OrdinalIgnoreCase) == true)
        {
            return new PlacesApiException(
                "Nieprawidłowy klucz API – Google go nie rozpoznaje. Sprawdź, czy został skopiowany w całości (bez spacji i cudzysłowów).");
        }

        var message = (httpStatus, status) switch
        {
            (HttpStatusCode.Forbidden, _) or (_, "PERMISSION_DENIED") =>
                "Brak dostępu do Places API (403). Sprawdź w Google Cloud Console, czy w projekcie " +
                "włączone jest \"Places API (New)\", podpięte jest konto rozliczeniowe (billing) " +
                "i czy ograniczenia klucza dopuszczają to API.",
            (HttpStatusCode.TooManyRequests, _) or (_, "RESOURCE_EXHAUSTED") =>
                "Przekroczony limit zapytań (429). Sprawdź limity (Quotas) i budżet w Google Cloud Console " +
                "albo spróbuj ponownie później.",
            (HttpStatusCode.Unauthorized, _) or (_, "UNAUTHENTICATED") =>
                "Google odrzuciło uwierzytelnienie (401). Sprawdź klucz API.",
            _ when (int)httpStatus >= 500 =>
                $"Po stronie Google wystąpił błąd serwera ({(int)httpStatus}). Spróbuj ponownie za chwilę.",
            _ =>
                $"Google Places API zwróciło błąd {(int)httpStatus}{(status is null ? "" : $" ({status})")}.",
        };

        return new PlacesApiException(message + details);
    }

    private static (string? Status, string? Message) TryParseGoogleError(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            if (!document.RootElement.TryGetProperty("error", out var error))
                return (null, null);

            return (GetString(error, "status"), GetString(error, "message"));
        }
        catch (JsonException)
        {
            return (null, null); // np. strona HTML z proxy – zostajemy przy kodzie HTTP
        }
    }

    private static string? GetString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
