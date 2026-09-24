using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using ModulithFoundry.Api.Authentication;

namespace ModulithFoundry.ApplicationTests;

public sealed class OidcTokenValidationTests
{
    [Theory]
    [InlineData("https://wrong-issuer.example", "modulith-foundry-bff", typeof(SecurityTokenInvalidIssuerException))]
    [InlineData("https://issuer.example", "wrong-audience", typeof(SecurityTokenInvalidAudienceException))]
    public async Task ValidateToken_InvalidIssuerOrAudience_IsRejected(
        string tokenIssuer,
        string tokenAudience,
        Type expectedException)
    {
        const string expectedIssuer = "https://issuer.example";
        const string expectedAudience = "modulith-foundry-bff";
        var signingKey = new SymmetricSecurityKey(RandomNumberGenerator.GetBytes(32));
        var handler = new JsonWebTokenHandler();
        string token = handler.CreateToken(new SecurityTokenDescriptor
        {
            Audience = tokenAudience,
            Expires = DateTime.UtcNow.AddMinutes(5),
            Issuer = tokenIssuer,
            SigningCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256),
            Subject = new ClaimsIdentity([new Claim("sub", "subject-1")]),
        });
        TokenValidationParameters validation = OidcTokenValidation.Create(expectedAudience);
        validation.ValidIssuer = expectedIssuer;
        validation.IssuerSigningKey = signingKey;

        TokenValidationResult result = await handler.ValidateTokenAsync(token, validation);

        Assert.False(result.IsValid);
        Assert.IsType(expectedException, result.Exception);
    }
}
