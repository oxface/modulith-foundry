using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;

namespace ModulithFoundry.Api.Authentication;

internal static class AuthenticationApi
{
    internal static IEndpointRouteBuilder MapAuthenticationApi(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
            "/auth/login",
            () =>
                Results.Challenge(
                    new AuthenticationProperties { RedirectUri = "/api/session" },
                    [OpenIdConnectDefaults.AuthenticationScheme]
                )
        );

        endpoints
            .MapPost(
                "/auth/logout",
                () =>
                    Results.SignOut(
                        new AuthenticationProperties { RedirectUri = "/" },
                        [
                            CookieAuthenticationDefaults.AuthenticationScheme,
                            OpenIdConnectDefaults.AuthenticationScheme,
                        ]
                    )
            )
            .RequireBffAntiforgery()
            .RequireAuthorization();

        endpoints
            .MapGet(
                "/api/session",
                (ClaimsPrincipal principal, HttpContext httpContext, IAntiforgery antiforgery) =>
                {
                    httpContext.Response.Headers.CacheControl = "no-store";
                    CurrentUser currentUser = principal.GetRequiredCurrentUser();
                    return TypedResults.Ok(
                        new SessionResponse(
                            currentUser.UserId.Value,
                            currentUser.Email,
                            currentUser.DisplayName,
                            antiforgery.GetAndStoreTokens(httpContext).RequestToken
                                ?? throw new InvalidOperationException(
                                    "No antiforgery request token was created."
                                )
                        )
                    );
                }
            )
            .RequireAuthorization();

        return endpoints;
    }

    private sealed record SessionResponse(
        Guid UserId,
        string? Email,
        string? DisplayName,
        string CsrfToken
    );
}
