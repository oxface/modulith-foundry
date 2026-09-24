using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;

namespace ModulithFoundry.Api.Authentication;

internal static class AuthenticationEndpoints
{
    internal static IEndpointRouteBuilder MapAuthenticationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/auth/login", () =>
            Results.Challenge(
                new AuthenticationProperties { RedirectUri = "/api/session" },
                [OpenIdConnectDefaults.AuthenticationScheme]));

        endpoints.MapPost("/auth/logout", async (
            HttpContext httpContext,
            IAntiforgery antiforgery) =>
        {
            if (!await antiforgery.IsRequestValidAsync(httpContext))
            {
                return Results.BadRequest();
            }

            return Results.SignOut(
                new AuthenticationProperties { RedirectUri = "/" },
                [
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    OpenIdConnectDefaults.AuthenticationScheme,
                ]);
        })
            .RequireAuthorization();

        endpoints.MapGet("/api/session", (
            ClaimsPrincipal principal,
            HttpContext httpContext,
            IAntiforgery antiforgery) =>
        {
            httpContext.Response.Headers.CacheControl = "no-store";
            return TypedResults.Ok(new SessionResponse(
                Guid.Parse(principal.GetRequiredClaimValue(ProductClaims.UserId)),
                principal.FindFirstValue("email"),
                principal.FindFirstValue("name"),
                antiforgery.GetAndStoreTokens(httpContext).RequestToken
                    ?? throw new InvalidOperationException("No antiforgery request token was created.")));
        })
            .RequireAuthorization();

        return endpoints;
    }

    private sealed record SessionResponse(
        Guid UserId,
        string? Email,
        string? DisplayName,
        string CsrfToken);
}
