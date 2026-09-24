using System.Security.Claims;

namespace ModulithFoundry.Api.Authentication;

internal static class ClaimsPrincipalExtensions
{
    internal static string GetRequiredClaimValue(this ClaimsPrincipal principal, string type)
    {
        ArgumentNullException.ThrowIfNull(principal);
        ArgumentException.ThrowIfNullOrWhiteSpace(type);

        return principal.FindFirstValue(type) is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException($"Authenticated principal has no '{type}' claim.");
    }
}
