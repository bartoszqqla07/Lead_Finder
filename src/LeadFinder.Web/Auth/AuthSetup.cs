using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;

namespace LeadFinder.Web.Auth;

/// <summary>
/// Logowanie jednym hasłem (aplikacja ma jednego użytkownika – właściciela).
/// Włącza się, gdy w konfiguracji jest ustawione <c>LeadFinder:Password</c> – na serwerze obowiązkowo,
/// lokalnie opcjonalnie. Bez hasła (lokalnie, tylko localhost) aplikacja działa bez logowania.
/// </summary>
public static class AuthSetup
{
    public const string PasswordKey = "LeadFinder:Password";
    public const string LoginRateLimitPolicy = "login";
    private const string CookieName = "leadfinder_auth";

    /// <summary>Czy logowanie jest włączone (ustawiono hasło).</summary>
    public static bool IsEnabled(IConfiguration configuration) =>
        !string.IsNullOrWhiteSpace(configuration[PasswordKey]);

    /// <summary>
    /// Rejestruje uwierzytelnianie ciasteczkiem, politykę "wszystko wymaga logowania" i limit prób logowania.
    /// </summary>
    /// <param name="services">Kontener usług.</param>
    /// <param name="configuration">Konfiguracja (hasło).</param>
    /// <param name="dataDirectory">Katalog danych – tam trafiają klucze szyfrujące ciasteczka.</param>
    public static void AddLeadFinderAuth(this IServiceCollection services, IConfiguration configuration, string dataDirectory)
    {
        var enabled = IsEnabled(configuration);

        services
            .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.Cookie.Name = CookieName;
                options.Cookie.HttpOnly = true;
                // Strict: przeglądarka nie wyśle ciasteczka z żądaniem zainicjowanym przez obcą stronę (ochrona przed CSRF).
                options.Cookie.SameSite = SameSiteMode.Strict;
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                // Długa sesja: na telefonie logujesz się raz, a nie przy każdym otwarciu.
                options.ExpireTimeSpan = TimeSpan.FromDays(30);
                options.SlidingExpiration = true;
                // API zwraca 401/403 zamiast przekierowania na stronę logowania – ekran logowania rysuje React.
                options.Events.OnRedirectToLogin = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                };
                options.Events.OnRedirectToAccessDenied = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                };
            });

        services.AddAuthorization(options =>
        {
            // Z hasłem: każdy endpoint wymaga zalogowania, chyba że jawnie oznaczono go AllowAnonymous.
            if (enabled)
                options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
        });

        if (enabled)
        {
            // Klucze szyfrujące ciasteczko muszą przetrwać restart serwera – inaczej każdy restart wylogowuje.
            services.AddDataProtection()
                .SetApplicationName("LeadFinder")
                .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(dataDirectory, "keys")));
        }

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            // Ochrona przed zgadywaniem hasła: 5 prób na minutę z jednego adresu IP.
            options.AddPolicy(LoginRateLimitPolicy, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(1) }));
        });
    }

    /// <summary>Porównanie hasła w stałym czasie (nie zdradza długości wspólnego prefiksu).</summary>
    public static bool PasswordMatches(string? candidate, string expected)
    {
        var candidateHash = SHA256.HashData(Encoding.UTF8.GetBytes(candidate ?? string.Empty));
        var expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(expected));
        return CryptographicOperations.FixedTimeEquals(candidateHash, expectedHash);
    }
}
