using System.Security.Claims;
using ModulithFoundry.Modules.Access.Contracts;

namespace ModulithFoundry.Api.Authentication;

internal sealed class CurrentUserCompletion(IExternalIdentityLinker identityLinker)
{
    internal async Task<CurrentUser> CompleteAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(principal);

        ExternalIdentity externalIdentity = ExternalIdentity.Create(
            principal.GetRequiredClaimValue("iss"),
            principal.GetRequiredClaimValue("sub"),
            principal.FindFirstValue("email"),
            principal.FindFirstValue("name"));
        UserIdentityLink linked = await identityLinker.LinkAsync(
            externalIdentity,
            cancellationToken);

        if (principal.Identity is not ClaimsIdentity claimsIdentity)
        {
            throw new InvalidOperationException("The validated OIDC identity is missing.");
        }

        ReplaceClaim(claimsIdentity, ProductClaims.UserId, linked.UserId.Value.ToString());
        ReplaceClaim(claimsIdentity, ProductClaims.Email, linked.Email);
        ReplaceClaim(claimsIdentity, ProductClaims.DisplayName, linked.DisplayName);

        return new CurrentUser(linked.UserId, linked.Email, linked.DisplayName);
    }

    private static void ReplaceClaim(ClaimsIdentity identity, string type, string? value)
    {
        foreach (Claim claim in identity.FindAll(type).ToArray())
        {
            identity.RemoveClaim(claim);
        }

        if (value is not null)
        {
            identity.AddClaim(new Claim(type, value));
        }
    }
}
