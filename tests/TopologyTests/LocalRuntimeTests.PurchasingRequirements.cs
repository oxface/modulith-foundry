using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Aspire.Hosting;
using Aspire.Hosting.Testing;

namespace ModulithFoundry.TopologyTests;

public sealed partial class LocalRuntimeTests
{
    private static readonly string[] AdministratorAndPurchasingAgentRoleIds =
    [
        "organization-administrator",
        "purchasing-agent",
    ];

    [Fact]
    public async Task PurchasingRequirements_CurrentPermission_ControlsScopedReadRoutes()
    {
        using var timeout = new CancellationTokenSource(StartupTimeout);
        IDistributedApplicationTestingBuilder builder = await CreateBuilderAsync(
            randomizePorts: true,
            timeout.Token
        );
        await using DistributedApplication app = await builder.BuildAsync(timeout.Token);
        await app.StartAsync(timeout.Token);
        await app.ResourceNotifications.WaitForResourceHealthyAsync("api", timeout.Token);
        var cookies = new CookieContainer();
        using var handler = CreateBrowserHandler(cookies);
        using var client = new HttpClient(handler)
        {
            BaseAddress = app.GetEndpoint("api", "https"),
        };
        string csrfToken = await SignInAsync(
            client,
            cookies,
            "alice",
            "topology-user-password",
            timeout.Token
        );
        Guid membershipId = await CreateOrganizationAndReadMembershipIdAsync(
            client,
            csrfToken,
            "Purchasing Queries",
            "purchasing-queries",
            timeout.Token
        );
        const string route = "/api/o/purchasing-queries/purchasing/requirements";
        using HttpResponseMessage denied = await client.GetAsync(route, timeout.Token);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        using HttpResponseMessage roles = await SendCommandAsync(
            client,
            HttpMethod.Put,
            $"/api/o/purchasing-queries/access/members/{membershipId:D}/roles",
            csrfToken,
            new { roleIds = AdministratorAndPurchasingAgentRoleIds },
            timeout.Token
        );
        roles.EnsureSuccessStatusCode();
        using HttpResponseMessage listed = await client.GetAsync(route, timeout.Token);
        listed.EnsureSuccessStatusCode();
        using JsonDocument requirements = JsonDocument.Parse(
            await listed.Content.ReadAsStringAsync(timeout.Token)
        );
        Assert.Equal(0, requirements.RootElement.GetArrayLength());
        using HttpResponseMessage missing = await client.GetAsync(route + "/1", timeout.Token);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        using HttpResponseMessage invalid = await client.GetAsync(
            route + "?limit=101",
            timeout.Token
        );
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal("application/problem+json", invalid.Content.Headers.ContentType?.MediaType);
        using HttpResponseMessage absentScope = await client.GetAsync(
            "/api/purchasing/requirements",
            timeout.Token
        );
        Assert.Equal(HttpStatusCode.NotFound, absentScope.StatusCode);
        using HttpResponseMessage revoke = await SendCommandAsync(
            client,
            HttpMethod.Put,
            $"/api/o/purchasing-queries/access/members/{membershipId:D}/roles",
            csrfToken,
            new { roleIds = SalesTestAdministratorRoleIds },
            timeout.Token
        );
        revoke.EnsureSuccessStatusCode();
        using HttpResponseMessage deniedAgain = await client.GetAsync(route + "/1", timeout.Token);
        Assert.Equal(HttpStatusCode.Forbidden, deniedAgain.StatusCode);
    }
}
