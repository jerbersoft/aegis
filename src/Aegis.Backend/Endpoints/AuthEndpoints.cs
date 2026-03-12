using System.Security.Claims;
using Aegis.Shared.Contracts.Auth;
using Aegis.Shared.Contracts.Common;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using NodaTime;

namespace Aegis.Backend.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/auth");

        group.MapPost("/login", async (LoginRequest request, HttpContext httpContext, IClock clock) =>
        {
            var username = string.IsNullOrWhiteSpace(request.Username) ? "operator" : request.Username.Trim();
            var authenticatedAt = clock.GetCurrentInstant();
            var authenticatedAtOffset = authenticatedAt.ToDateTimeOffset();

            var claims = new[]
            {
                new Claim(ClaimTypes.Name, username),
                new Claim(ClaimTypes.NameIdentifier, username)
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await httpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties
                {
                    IsPersistent = true,
                    IssuedUtc = authenticatedAtOffset,
                    ExpiresUtc = authenticatedAtOffset.AddHours(8)
                });

            return Results.Ok(new SessionView(username, true, authenticatedAt));
        }).AllowAnonymous();

        group.MapPost("/logout", async (HttpContext httpContext) =>
        {
            await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.NoContent();
        });

        group.MapGet("/session", (ClaimsPrincipal user, IClock clock) =>
        {
            if (user.Identity?.IsAuthenticated != true)
            {
                return Results.Unauthorized();
            }

            return Results.Ok(new SessionView(
                user.Identity.Name ?? "operator",
                true,
                clock.GetCurrentInstant()));
        });

        return endpoints;
    }
}
