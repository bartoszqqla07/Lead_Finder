using LeadFinder.Models;
using LeadFinder.Services;
using LeadFinder.Web.Contracts;
using LeadFinder.Web.Data;

namespace LeadFinder.Web.Settings;

/// <summary>Odczyt i zapis ustawień aplikacji (klucz API, podpis w szkicach wiadomości).</summary>
public sealed class SettingsService(LeadFinderDbContext db, AppPaths paths)
{
    public const string ApiKeyVariable = "GOOGLE_PLACES_API_KEY";

    /// <summary>
    /// Darmowy miesięczny limit dla SKU Text Search Enterprise (pola websiteUri, rating, telefon) według
    /// cennika Google Maps Platform z marca 2025. Można go zmienić w Ustawieniach, gdy Google zmieni cennik.
    /// </summary>
    public const int DefaultFreeMonthlyRequests = 1000;

    /// <summary>Wiersz ustawień (tworzony, gdyby zniknął z bazy).</summary>
    public async Task<AppSettingsEntity> GetEntityAsync(CancellationToken cancellationToken = default)
    {
        var settings = await db.Settings.FindAsync([AppSettingsEntity.SingletonId], cancellationToken);
        if (settings is not null)
            return settings;

        settings = new AppSettingsEntity();
        db.Settings.Add(settings);
        await db.SaveChangesAsync(cancellationToken);
        return settings;
    }

    /// <summary>
    /// Klucz API: zmienna środowiskowa (lub .env) ma pierwszeństwo przed kluczem wpisanym w aplikacji.
    /// </summary>
    public async Task<(string? Key, ApiKeySource Source)> GetApiKeyAsync(CancellationToken cancellationToken = default)
    {
        var fromEnvironment = Environment.GetEnvironmentVariable(ApiKeyVariable);
        if (!string.IsNullOrWhiteSpace(fromEnvironment))
            return (fromEnvironment.Trim(), ApiKeySource.Environment);

        var settings = await GetEntityAsync(cancellationToken);
        return string.IsNullOrWhiteSpace(settings.GooglePlacesApiKey)
            ? (null, ApiKeySource.None)
            : (settings.GooglePlacesApiKey, ApiKeySource.App);
    }

    /// <summary>MessageDrafter z danymi nadawcy z ustawień.</summary>
    public async Task<MessageDrafter> CreateMessageDrafterAsync(CancellationToken cancellationToken = default)
    {
        var settings = await GetEntityAsync(cancellationToken);
        return new MessageDrafter(new SenderProfile(
            settings.SenderName, settings.Signature, settings.ContactEmail, settings.PostalAddress));
    }

    public async Task<SettingsDto> GetDtoAsync(CancellationToken cancellationToken = default)
    {
        var settings = await GetEntityAsync(cancellationToken);
        var (key, source) = await GetApiKeyAsync(cancellationToken);

        return new SettingsDto(
            HasApiKey: key is not null,
            ApiKeySource: source,
            ApiKeyHint: key is null ? null : MaskKey(key),
            SenderName: settings.SenderName ?? string.Empty,
            Signature: settings.Signature ?? string.Empty,
            ContactEmail: settings.ContactEmail ?? string.Empty,
            PostalAddress: settings.PostalAddress ?? string.Empty,
            FreeMonthlyRequests: settings.FreeMonthlyRequests ?? DefaultFreeMonthlyRequests,
            DefaultSenderName: MessageDrafter.DefaultSenderName,
            DefaultSignature: MessageDrafter.DefaultSignature,
            DataDirectory: paths.DataDirectory);
    }

    public async Task<SettingsDto> UpdateAsync(UpdateSettingsRequest request, CancellationToken cancellationToken = default)
    {
        var settings = await GetEntityAsync(cancellationToken);

        if (request.ApiKey is not null)
            settings.GooglePlacesApiKey = string.IsNullOrWhiteSpace(request.ApiKey) ? null : request.ApiKey.Trim();
        if (request.SenderName is not null)
            settings.SenderName = NullIfEmpty(request.SenderName);
        if (request.Signature is not null)
            settings.Signature = NullIfEmpty(request.Signature.Replace("\r\n", "\n"));
        if (request.ContactEmail is not null)
            settings.ContactEmail = NullIfEmpty(request.ContactEmail);
        if (request.FreeMonthlyRequests is { } limit)
            settings.FreeMonthlyRequests = Math.Clamp(limit, 0, 1_000_000);
        if (request.PostalAddress is not null)
            settings.PostalAddress = NullIfEmpty(request.PostalAddress.Replace("\r\n", "\n"));

        await db.SaveChangesAsync(cancellationToken);
        return await GetDtoAsync(cancellationToken);
    }

    /// <summary>Pokazuje tylko początek i koniec klucza, np. "AIza…x3Fq".</summary>
    private static string MaskKey(string key) =>
        key.Length <= 10 ? new string('•', key.Length) : $"{key[..4]}…{key[^4..]}";

    private static string? NullIfEmpty(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>Katalog danych aplikacji (baza SQLite).</summary>
public sealed record AppPaths(string DataDirectory)
{
    public string DatabasePath => Path.Combine(DataDirectory, "leadfinder.db");
}
