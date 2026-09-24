using System.Security.Claims;
using ModulithFoundry.Api.Authentication;
using ModulithFoundry.Modules.Access.Contracts;

namespace ModulithFoundry.ApplicationTests;

public sealed class CurrentUserCompletionTests
{
    [Fact]
    public async Task CompleteCurrentUser_ValidatedOidcPrincipal_UsesLinkedProductUser()
    {
        var userId = new UserId(Guid.Parse("01997d3d-8d8c-7c31-b74b-8dafc2bb2a01"));
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim("iss", "https://issuer.example"),
                new Claim("sub", "external-subject"),
                new Claim("email", " provider@example.test "),
                new Claim("name", " Provider Name "),
                new Claim(ProductClaims.UserId, "01997d4b-99a4-7e12-bf9a-8fd4cdbaf012"),
                new Claim(ProductClaims.Email, "stale@example.test"),
                new Claim(ProductClaims.DisplayName, "Stale User"),
            ],
            "oidc"));
        var completion = new CurrentUserCompletion(new LinkedUserStub(userId));

        CurrentUser completed = await completion.CompleteAsync(
            principal,
            TestContext.Current.CancellationToken);
        CurrentUser restored = principal.GetRequiredCurrentUser();

        var expected = new CurrentUser(
            userId,
            "linked@example.test",
            "Linked User");
        Assert.Equal(expected, completed);
        Assert.Equal(expected, restored);
    }

    private sealed class LinkedUserStub(UserId userId) : IExternalIdentityLinker
    {
        public Task<UserIdentityLink> LinkAsync(
            ExternalIdentity identity,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new UserIdentityLink(
                userId,
                "linked@example.test",
                "Linked User"));
    }
}
