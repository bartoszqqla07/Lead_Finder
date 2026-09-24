using System.Security.Claims;
using LeadFinder.Web.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace LeadFinder.Web.Endpoints;

public sealed record LoginRequest(string? Password);

public sealed record AuthStatusDto(bool AuthRequired, bool Authenticated);

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").AllowAnonymous();
        group.MapGet("/status", GetStatus);
        group.MapPost("/login", LoginAsync).RequireRateLimiting(AuthSetup.LoginRateLimitPolicy);
        group.MapPost("/logout", LogoutAsync);
    }

    private static AuthStatusDto GetStatus(HttpContext http, IConfiguration configuration) =>
        new(AuthSetup.IsEnabled(configuration), http.User.Identity?.IsAuthenticated ?? false);

    private static async Task<IResult> LoginAsync(LoginRequest body, HttpContext http, IConfiguration configuration)
    {
        if (!AuthSetup.IsEnabled(configuration))
            return Results.NoContent();

        if (!AuthSetup.PasswordMatches(body.Password, configuration[AuthSetup.PasswordKey]!))
            return Results.Problem("Nieprawidłowe hasło.", statusCode: StatusCodes.Status401Unauthorized);

        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, "owner")], CookieAuthenticationDefaults.AuthenticationScheme);
        await http.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = true });

        return Results.NoContent();
    }

    // Sam HttpContext w parametrach = surowy RequestDelegate, więc status ustawiamy bezpośrednio.
    private static async Task LogoutAsync(HttpContext http)
    {
        await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        http.Response.StatusCode = StatusCodes.Status204NoContent;
    }
}
