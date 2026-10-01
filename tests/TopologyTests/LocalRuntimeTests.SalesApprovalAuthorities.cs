using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Aspire.Hosting;
using Aspire.Hosting.Testing;

namespace ModulithFoundry.TopologyTests;

public sealed partial class LocalRuntimeTests
{
    private static readonly string[] ApprovalAuthorityManagerRoleIds =
    [
        "organization-administrator",
        "sales-manager",
    ];

    [Fact]
    public async Task SalesApprovalAuthority_AuthenticatedManager_ManagesVersionedTenantAuthority()
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
        string csrf = await SignInAsync(
            client,
            cookies,
            "alice",
            "topology-user-password",
            timeout.Token
        );
        Guid membershipId = await CreateOrganizationAndReadMembershipIdAsync(
            client,
            csrf,
            "Authority",
            "authority",
            timeout.Token
        );
        string url = $"/api/o/authority/sales/approval-authorities/{membershipId:D}";
        using HttpResponseMessage denied = await SendCommandAsync(
            client,
            HttpMethod.Put,
            url,
            csrf,
            new
            {
                maximumAmount = 100m,
                currency = "USD",
                expectedVersion = 0,
            },
            timeout.Token
        );
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        using HttpResponseMessage roles = await SendCommandAsync(
            client,
            HttpMethod.Put,
            $"/api/o/authority/access/members/{membershipId:D}/roles",
            csrf,
            new { roleIds = ApprovalAuthorityManagerRoleIds },
            timeout.Token
        );
        roles.EnsureSuccessStatusCode();
        using HttpResponseMessage noCsrf = await client.PutAsJsonAsync(
            url,
            new
            {
                maximumAmount = 100m,
                currency = "USD",
                expectedVersion = 0,
            },
            timeout.Token
        );
        Assert.Equal(HttpStatusCode.BadRequest, noCsrf.StatusCode);
        using HttpResponseMessage created = await SendCommandAsync(
            client,
            HttpMethod.Put,
            url,
            csrf,
            new
            {
                maximumAmount = 100.25m,
                currency = " usd ",
                expectedVersion = 0,
            },
            timeout.Token
        );
        created.EnsureSuccessStatusCode();
        using JsonDocument createdJson = JsonDocument.Parse(
            await created.Content.ReadAsStringAsync(timeout.Token)
        );
        Assert.Equal(membershipId, createdJson.RootElement.GetProperty("membershipId").GetGuid());
        Assert.Equal("USD", createdJson.RootElement.GetProperty("currency").GetString());
        Assert.Equal(100.25m, createdJson.RootElement.GetProperty("maximumAmount").GetDecimal());
        Assert.Equal(1, createdJson.RootElement.GetProperty("version").GetInt64());
        using HttpResponseMessage found = await client.GetAsync(url, timeout.Token);
        found.EnsureSuccessStatusCode();
        using HttpResponseMessage invalid = await SendCommandAsync(
            client,
            HttpMethod.Put,
            url,
            csrf,
            new
            {
                maximumAmount = -1m,
                currency = "USD",
                expectedVersion = 1,
            },
            timeout.Token
        );
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal("application/problem+json", invalid.Content.Headers.ContentType?.MediaType);
        using JsonDocument invalidJson = JsonDocument.Parse(
            await invalid.Content.ReadAsStringAsync(timeout.Token)
        );
        Assert.Equal("maximumAmount", invalidJson.RootElement.GetProperty("field").GetString());
        using HttpResponseMessage stale = await SendCommandAsync(
            client,
            HttpMethod.Put,
            url,
            csrf,
            new
            {
                maximumAmount = 200m,
                currency = "EUR",
                expectedVersion = 0,
            },
            timeout.Token
        );
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using HttpResponseMessage revoked = await SendCommandAsync(
            client,
            HttpMethod.Post,
            url + "/revoke",
            csrf,
            new { expectedVersion = 1 },
            timeout.Token
        );
        revoked.EnsureSuccessStatusCode();
        using JsonDocument revokedJson = JsonDocument.Parse(
            await revoked.Content.ReadAsStringAsync(timeout.Token)
        );
        Assert.False(revokedJson.RootElement.GetProperty("isEnabled").GetBoolean());
        Assert.Equal(2, revokedJson.RootElement.GetProperty("version").GetInt64());
        using HttpResponseMessage repeated = await SendCommandAsync(
            client,
            HttpMethod.Post,
            url + "/revoke",
            csrf,
            new { expectedVersion = 2 },
            timeout.Token
        );
        repeated.EnsureSuccessStatusCode();
        Guid otherMembership = await CreateOrganizationAndReadMembershipIdAsync(
            client,
            csrf,
            "Other Authority",
            "other-authority",
            timeout.Token
        );
        using HttpResponseMessage otherRoles = await SendCommandAsync(
            client,
            HttpMethod.Put,
            $"/api/o/other-authority/access/members/{otherMembership:D}/roles",
            csrf,
            new { roleIds = ApprovalAuthorityManagerRoleIds },
            timeout.Token
        );
        otherRoles.EnsureSuccessStatusCode();
        using HttpResponseMessage foreign = await client.GetAsync(
            $"/api/o/other-authority/sales/approval-authorities/{membershipId:D}",
            timeout.Token
        );
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        using HttpResponseMessage roleRemoval = await SendCommandAsync(
            client,
            HttpMethod.Put,
            $"/api/o/authority/access/members/{membershipId:D}/roles",
            csrf,
            new { roleIds = SalesTestAdministratorRoleIds },
            timeout.Token
        );
        roleRemoval.EnsureSuccessStatusCode();
        using HttpResponseMessage deniedRead = await client.GetAsync(url, timeout.Token);
        Assert.Equal(HttpStatusCode.Forbidden, deniedRead.StatusCode);
        using HttpResponseMessage deniedRevoke = await SendCommandAsync(
            client,
            HttpMethod.Post,
            url + "/revoke",
            csrf,
            new { expectedVersion = 2 },
            timeout.Token
        );
        Assert.Equal(HttpStatusCode.Forbidden, deniedRevoke.StatusCode);
    }
}
