using LeadFinder.Common;
using LeadFinder.Config;
using LeadFinder.Services;
using LeadFinder.Web.Contracts;
using LeadFinder.Web.Settings;

namespace LeadFinder.Web.Endpoints;

public static class SettingsEndpoints
{
    public static void MapSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/categories", (CategoryCatalog catalog) =>
            catalog.Categories.Select(c => new CategoryDto(c.Id, c.Query, c.EnglishName)));

        app.MapGet("/api/regions", (RegionCatalog regions) =>
            regions.Regions.Select(r => new RegionDto(r.Id, r.Name, r.Cities)));

        app.MapGet("/api/usage", (UsageService usage, CancellationToken ct) => usage.GetCurrentMonthAsync(ct));

        var group = app.MapGroup("/api/settings");
        group.MapGet("", (SettingsService settings, CancellationToken ct) => settings.GetDtoAsync(ct));
        group.MapPut("", (UpdateSettingsRequest body, SettingsService settings, CancellationToken ct) => settings.UpdateAsync(body, ct));
        group.MapPost("/verify-api-key", VerifyApiKeyAsync);
    }

    private static async Task<VerifyApiKeyResultDto> VerifyApiKeyAsync(
        SettingsService settings, IHttpClientFactory httpClientFactory, CancellationToken ct)
    {
        var (apiKey, _) = await settings.GetApiKeyAsync(ct);
        if (apiKey is null)
            return new VerifyApiKeyResultDto(false, "Nie ustawiono klucza API.");

        try
        {
            var client = new PlacesApiClient(httpClientFactory.CreateClient(), apiKey);
            await client.VerifyApiKeyAsync(ct);
            return new VerifyApiKeyResultDto(true, "Klucz działa – Google Places API (New) odpowiada poprawnie.");
        }
        catch (LeadFinderException ex)
        {
            return new VerifyApiKeyResultDto(false, ex.Message);
        }
    }
}
