using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using ModulithFoundry.Api.Authentication;

namespace ModulithFoundry.Api.Modules.Access.Invitations;

internal static class InvitationAcceptanceEndpoints
{
    internal static IEndpointRouteBuilder MapInvitationAcceptanceEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/invitations/accept", StartAcceptanceAsync);
        endpoints.MapPost("/invitations/accept/retry", RetryAcceptanceAsync)
            .RequireBffAntiforgery()
            .RequireAuthorization();
        endpoints.MapGet("/invitations/accept/resume", ResumeAcceptanceAsync);
        endpoints.MapGet("/invitations/accept/result", AcceptanceResult);
        return endpoints;
    }

    private static IResult AcceptanceResult(
        string? status,
        string? acceptanceHandle,
        HttpContext httpContext)
    {
        SetSensitiveResponseHeaders(httpContext);
        return InvitationAcceptanceNavigation.IsRecipientMismatch(status)
            ? Results.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "The authenticated identity cannot accept this invitation.",
                extensions: string.IsNullOrWhiteSpace(acceptanceHandle)
                    ? null
                    : new Dictionary<string, object?>
                    {
                        ["retry"] = $"/invitations/accept/retry?acceptanceHandle={Uri.EscapeDataString(acceptanceHandle)}",
                        ["retryMethod"] = HttpMethods.Post,
                    })
            : Results.Problem(
                statusCode: StatusCodes.Status410Gone,
                title: "This invitation is unavailable.");
    }

    private static async Task<IResult> StartAcceptanceAsync(
        Guid invitationId,
        string code,
        HttpContext httpContext,
        RedisPendingInvitationAcceptanceStore pendingAcceptances,
        CancellationToken cancellationToken)
    {
        SetSensitiveResponseHeaders(httpContext);

        if (invitationId == Guid.Empty || string.IsNullOrWhiteSpace(code))
        {
            return Results.NotFound();
        }

        string acceptanceHandle = await pendingAcceptances.CreateAsync(
            new PendingInvitationAcceptance(invitationId, code),
            cancellationToken);
        return Challenge(acceptanceHandle);
    }

    private static async Task<IResult> RetryAcceptanceAsync(
        string acceptanceHandle,
        HttpContext httpContext,
        RedisPendingInvitationAcceptanceStore pendingAcceptances,
        CancellationToken cancellationToken)
    {
        SetSensitiveResponseHeaders(httpContext);
        if (await pendingAcceptances.RetrieveAsync(acceptanceHandle, cancellationToken) is null)
        {
            return Results.StatusCode(StatusCodes.Status410Gone);
        }

        var properties = new AuthenticationProperties
        {
            RedirectUri = $"/invitations/accept/resume?acceptanceHandle={Uri.EscapeDataString(acceptanceHandle)}",
        };
        return Results.SignOut(
            properties,
            [
                CookieAuthenticationDefaults.AuthenticationScheme,
                OpenIdConnectDefaults.AuthenticationScheme,
            ]);
    }

    private static async Task<IResult> ResumeAcceptanceAsync(
        string acceptanceHandle,
        HttpContext httpContext,
        RedisPendingInvitationAcceptanceStore pendingAcceptances,
        CancellationToken cancellationToken)
    {
        SetSensitiveResponseHeaders(httpContext);
        return await pendingAcceptances.RetrieveAsync(acceptanceHandle, cancellationToken) is null
            ? Results.StatusCode(StatusCodes.Status410Gone)
            : Challenge(acceptanceHandle);
    }

    private static IResult Challenge(string acceptanceHandle)
    {
        var properties = new AuthenticationProperties
        {
            RedirectUri = "/api/session",
        };
        properties.Items[InvitationAuthenticationProperties.AcceptanceHandle] = acceptanceHandle;
        return Results.Challenge(
            properties,
            [OpenIdConnectDefaults.AuthenticationScheme]);
    }

    private static void SetSensitiveResponseHeaders(HttpContext httpContext)
    {
        httpContext.Response.Headers.CacheControl = "no-store";
        httpContext.Response.Headers.Append("Referrer-Policy", "no-referrer");
    }
}
