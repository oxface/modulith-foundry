using Microsoft.IdentityModel.Tokens;

namespace ModulithFoundry.Api.Authentication;

internal static class OidcTokenValidation
{
    internal static TokenValidationParameters Create(string clientId) =>
        new()
        {
            NameClaimType = "name",
            ValidateAudience = true,
            ValidateIssuer = true,
            ValidAudience = clientId,
        };
}
