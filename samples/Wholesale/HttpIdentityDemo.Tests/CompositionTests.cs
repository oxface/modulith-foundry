using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ModulithFoundry.ActorIdentity;

namespace ModulithFoundry.Samples.Wholesale.HttpIdentityDemo.Tests;

public sealed class CompositionTests
{
    [Theory]
    [InlineData("https://identity.test", "application-alpha")]
    [InlineData("https://other-identity.test", "application-beta")]
    public async Task ValidatedExternalPairMapsToApplicationUserWithoutUsingEmail(
        string issuer,
        string user
    )
    {
        await using var app = await StartAsync();
        using var client = app.GetTestClient();
        using var request = Request(app, "/identity", issuer);
        using var response = await client.SendAsync(request, Token);
        response.EnsureSuccessStatusCode();
        Assert.Equal(
            new IdentityResponse(ActorKind.Human, user),
            await response.Content.ReadFromJsonAsync<IdentityResponse>(Token)
        );
        using var publicRequest = Request(app, "/public-identity", issuer);
        using var publicResponse = await client.SendAsync(publicRequest, Token);
        Assert.Equal(
            new IdentityResponse(ActorKind.Human, user),
            await publicResponse.Content.ReadFromJsonAsync<IdentityResponse>(Token)
        );
    }

    [Fact]
    public async Task AnonymousPublicIdentityIsExplicitAndProtectedIdentityKeepsNativeChallenge()
    {
        await using var app = await StartAsync();
        using var client = app.GetTestClient();
        using var anonymous = await client.GetAsync("/public-identity", Token);
        Assert.Equal(
            new IdentityResponse(ActorKind.Anonymous, null),
            await anonymous.Content.ReadFromJsonAsync<IdentityResponse>(Token)
        );
        using var protectedResponse = await client.GetAsync("/identity", Token);
        Assert.Equal(HttpStatusCode.Unauthorized, protectedResponse.StatusCode);
    }

    [Theory]
    [InlineData("/identity", false)]
    [InlineData("/public-identity", false)]
    [InlineData("/public-identity", true)]
    public async Task UnknownOrAmbiguousExternalIdentityUsesConsumerMappingRejection(
        string path,
        bool duplicateSubject
    )
    {
        await using var app = await StartAsync();
        using var client = app.GetTestClient();
        using var request = Request(
            app,
            path,
            duplicateSubject ? "https://identity.test" : "https://unknown.test",
            duplicateSubject
        );
        using var response = await client.SendAsync(request, Token);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("Actor mapping failed.", await response.Content.ReadAsStringAsync(Token));
    }

    private static async Task<WebApplication> StartAsync()
    {
        var builder = WebApplication.CreateBuilder(
            new WebApplicationOptions { EnvironmentName = "Testing" }
        );
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Oidc:Authority"] = "https://identity.test",
                ["Oidc:ClientId"] = "configured-demo",
                ["IdentityDirectory:0:Issuer"] = "https://identity.test",
                ["IdentityDirectory:0:Subject"] = "shared-subject",
                ["IdentityDirectory:0:UserId"] = "application-alpha",
                ["IdentityDirectory:1:Issuer"] = "https://other-identity.test",
                ["IdentityDirectory:1:Subject"] = "shared-subject",
                ["IdentityDirectory:1:UserId"] = "application-beta",
            }
        );
        DemoComposition.AddIdentityServices(builder.Services, builder.Configuration);
        builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
        // Cookie round trips prove this slice; no test contacts a personal OIDC provider.
        builder.Services.PostConfigure<AuthenticationOptions>(options =>
            options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme
        );
        var app = builder.Build();
        DemoComposition.ConfigureHttp(app);
        await app.StartAsync(Token);
        return app;
    }

    private static HttpRequestMessage Request(
        WebApplication app,
        string path,
        string issuer,
        bool duplicateSubject = false
    )
    {
        var claims = new List<Claim>
        {
            new("iss", issuer),
            new("sub", "shared-subject"),
            new("email", "same-person@example.test"),
        };
        if (duplicateSubject)
            claims.Add(new Claim("sub", "another-subject"));
        var identity = new ClaimsIdentity(claims, "fixture-cookie");
        var oidc = app
            .Services.GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>()
            .Get("oidc");
        using var payload = JsonSerializer.SerializeToDocument(
            new
            {
                iss = issuer,
                sub = "shared-subject",
                email = "same-person@example.test",
            }
        );
        foreach (var action in oidc.ClaimActions)
            action.Run(payload.RootElement, identity, issuer);
        var principal = new ClaimsPrincipal(identity);
        var options = app
            .Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(CookieAuthenticationDefaults.AuthenticationScheme);
        var ticket = new AuthenticationTicket(
            principal,
            CookieAuthenticationDefaults.AuthenticationScheme
        );
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add(
            "Cookie",
            options.Cookie.Name + "=" + options.TicketDataFormat.Protect(ticket)
        );
        return request;
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;
}
