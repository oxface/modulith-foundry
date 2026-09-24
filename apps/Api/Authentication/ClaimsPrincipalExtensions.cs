using System.Security.Claims;
using ModulithFoundry.Modules.Access.Contracts;

namespace ModulithFoundry.Api.Authentication;

internal static class ClaimsPrincipalExtensions
{
    internal static CurrentUser GetRequiredCurrentUser(this ClaimsPrincipal principal)
    {
        string userIdClaim = principal.GetRequiredClaimValue(ProductClaims.UserId);
        if (!Guid.TryParse(userIdClaim, out Guid userId))
        {
            throw new InvalidOperationException("Authenticated principal has an invalid product User ID.");
        }

        return new CurrentUser(
            new UserId(userId),
            principal.FindFirstValue(ProductClaims.Email),
            principal.FindFirstValue(ProductClaims.DisplayName));
    }

    internal static string GetRequiredClaimValue(this ClaimsPrincipal principal, string type)
    {
        ArgumentNullException.ThrowIfNull(principal);
        ArgumentException.ThrowIfNullOrWhiteSpace(type);

        return principal.FindFirstValue(type) is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException($"Authenticated principal has no '{type}' claim.");
    }
}
