using System.Security.Claims;
using ModulithFoundry.Modules.Access.Contracts;

namespace ModulithFoundry.Api.Authentication;

internal sealed class CurrentUserCompletion(IExternalIdentityLinking identityLinking)
{
    internal async Task<CompletedOidcIdentity> CompleteAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(principal);

        ExternalIdentity externalIdentity = ExternalIdentity.Create(
            principal.GetRequiredClaimValue("iss"),
            principal.GetRequiredClaimValue("sub"),
            principal.FindFirstValue("email"),
            principal.FindFirstValue("name"));
        UserIdentityLink linked = await identityLinking.LinkAsync(
            externalIdentity,
            cancellationToken);

        if (principal.Identity is not ClaimsIdentity claimsIdentity)
        {
            throw new InvalidOperationException("The validated OIDC identity is missing.");
        }

        ReplaceClaim(claimsIdentity, ProductClaims.UserId, linked.UserId.Value.ToString());
        ReplaceClaim(claimsIdentity, ProductClaims.Email, linked.Email);
        ReplaceClaim(claimsIdentity, ProductClaims.DisplayName, linked.DisplayName);
        string? verifiedProviderEmail = string.Equals(
            principal.FindFirstValue("email_verified"),
            "true",
            StringComparison.OrdinalIgnoreCase)
            ? externalIdentity.Email
            : null;
        RemoveClaims(claimsIdentity, "email_verified");
        return new CompletedOidcIdentity(
            new CurrentUser(
                linked.UserId,
                linked.Email,
                linked.DisplayName),
            verifiedProviderEmail);
    }

    private static void ReplaceClaim(ClaimsIdentity identity, string type, string? value)
    {
        RemoveClaims(identity, type);

        if (value is not null)
        {
            identity.AddClaim(new Claim(type, value));
        }
    }

    private static void RemoveClaims(ClaimsIdentity identity, string type)
    {
        foreach (Claim claim in identity.FindAll(type).ToArray())
        {
            identity.RemoveClaim(claim);
        }
    }
}
