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
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ModulithFoundry.ActorIdentity;
using ModulithFoundry.Samples.Wholesale.HttpIdentityDemo.Access;
using ModulithFoundry.Samples.Wholesale.HttpIdentityDemo.Inventory.Contracts;
using ModulithFoundry.Tenancy;
using ModulithFoundry.Tenancy.AspNetCore;

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
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(Token);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(403, problem!.Status);
        Assert.Equal("Actor mapping failed.", problem.Title);
    }

    [Fact]
    public async Task SameActorSelectsDifferentOrganizationsInSeparateRequests()
    {
        await using var app = await StartAsync();
        using var client = app.GetTestClient();
        foreach (
            var (organization, tenant) in new[]
            {
                ("north-supply", "wholesale-alpha"),
                ("south-supply", "wholesale-beta"),
            }
        )
        {
            using var request = Request(
                app,
                $"/organizations/{organization}/identity",
                "https://identity.test"
            );
            using var response = await client.SendAsync(request, Token);
            response.EnsureSuccessStatusCode();
            Assert.Equal(
                new TenantIdentityResponse(ActorKind.Human, "application-alpha", tenant),
                await response.Content.ReadFromJsonAsync<TenantIdentityResponse>(Token)
            );
        }
    }

    [Theory]
    [InlineData("north-supply", "wholesale-alpha", 42)]
    [InlineData("south-supply", "wholesale-beta", 7)]
    public async Task AnonymousCatalogReadsUseSelectedTenantAndInventoryContract(
        string organization,
        string tenant,
        int quantity
    )
    {
        await using var app = await StartAsync();
        using var client = app.GetTestClient();
        var result = await client.GetFromJsonAsync<CatalogResponse>(
            $"/organizations/{organization}/catalog",
            Token
        );
        Assert.Equal(
            new CatalogResponse(
                new TenantIdentityResponse(ActorKind.Anonymous, null, tenant),
                new StockAvailability("DEMO-NOTEBOOK", quantity)
            ),
            result
        );
    }

    [Theory]
    [InlineData("NORTH-SUPPLY.WHOLESALE.EXAMPLE.TEST.:8443", "wholesale-alpha", 42)]
    [InlineData("south-supply.wholesale.example.test", "wholesale-beta", 7)]
    public async Task ConfiguredSubdomainAlternativeUsesSameCanonicalDirectory(
        string host,
        string tenant,
        int quantity
    )
    {
        await using var app = await StartAsync(subdomain: true);
        using var client = app.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/catalog");
        request.Headers.Host = host;
        using var response = await client.SendAsync(request, Token);
        response.EnsureSuccessStatusCode();
        Assert.Equal(
            new CatalogResponse(
                new TenantIdentityResponse(ActorKind.Anonymous, null, tenant),
                new StockAvailability("DEMO-NOTEBOOK", quantity)
            ),
            await response.Content.ReadFromJsonAsync<CatalogResponse>(Token)
        );
    }

    [Theory]
    [InlineData("wholesale.example.test", 400)]
    [InlineData("unknown.wholesale.example.test", 404)]
    [InlineData("nested.north-supply.wholesale.example.test", 404)]
    [InlineData("north-supply.other.example.test", 400)]
    public async Task SubdomainSelectionAndNativeHostFilteringRejectUnsafeCatalogRequests(
        string host,
        int status
    )
    {
        await using var app = await StartAsync(subdomain: true);
        using var client = app.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/catalog");
        request.Headers.Host = host;
        using var response = await client.SendAsync(request, Token);
        Assert.Equal(status, (int)response.StatusCode);
        if (host != "north-supply.other.example.test")
        {
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(Token);
            Assert.Equal(status, problem!.Status);
            Assert.Equal(
                status == 400 ? "Select an Organization." : "Organization selection failed.",
                problem.Title
            );
        }
    }

    [Fact]
    public async Task UnknownOrganizationGetsConsumerFailureWhileProtectedReadKeepsNativeChallenge()
    {
        await using var app = await StartAsync();
        using var client = app.GetTestClient();
        using var unknown = await client.GetAsync("/organizations/unknown/catalog", Token);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Contains(
            "Organization selection failed.",
            await unknown.Content.ReadAsStringAsync(Token)
        );
        using var protectedResponse = await client.GetAsync(
            "/organizations/north-supply/identity",
            Token
        );
        Assert.Equal(HttpStatusCode.Unauthorized, protectedResponse.StatusCode);
        using var healthy = await client.GetAsync("/health", Token);
        healthy.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task InventoryContractRequiresTenantForCallsOutsideHttp()
    {
        await using var app = await StartAsync();
        using var scope = app.Services.CreateScope();
        scope
            .ServiceProvider.GetRequiredService<ITenantContextInitializer>()
            .Initialize(TenantContext.Tenantless());
        var catalog = scope.ServiceProvider.GetRequiredService<IStockCatalog>();
        Assert.Throws<TenantRequiredException>(() => catalog.Read());
    }

    private static async Task<WebApplication> StartAsync(bool subdomain = false)
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
                ["Organizations:0:Slug"] = "north-supply",
                ["Organizations:0:Id"] = "wholesale-alpha",
                ["Organizations:1:Slug"] = "south-supply",
                ["Organizations:1:Id"] = "wholesale-beta",
            }
        );
        DemoComposition.AddServices(builder.Services, builder.Configuration);
        if (subdomain)
        {
            builder.Services.AddOrganizationTenancyFromSubdomain("wholesale.example.test.");
            builder.Services.AddHostFiltering(options =>
                options.UseTenantSubdomainHosts("wholesale.example.test.")
            );
        }
        else
            builder.Services.AddOrganizationTenancyFromRoute("organization");
        builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
        // Cookie round trips prove this slice; no test contacts a personal OIDC provider.
        builder.Services.PostConfigure<AuthenticationOptions>(options =>
            options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme
        );
        var app = builder.Build();
        DemoComposition.ConfigureHttp(app);
        DemoComposition.MapOrganizationEndpoints(
            app,
            subdomain ? "/catalog" : "/organizations/{organization}/catalog",
            subdomain ? "/tenant-identity" : "/organizations/{organization}/identity"
        );
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
