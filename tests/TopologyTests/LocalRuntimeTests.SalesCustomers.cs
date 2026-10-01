using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Aspire.Hosting;
using Aspire.Hosting.Testing;

namespace ModulithFoundry.TopologyTests;

public sealed partial class LocalRuntimeTests
{
    private static readonly string[] AdministratorAndSalesClerkRoleIds = ["organization-administrator", "sales-clerk"];
    private static readonly string[] SalesTestAdministratorRoleIds = ["organization-administrator"];
    [Fact]
    public async Task SalesCustomers_AuthenticatedClerk_CreatesAndLooksUpWithinOrganization()
    {
        using var timeout = new CancellationTokenSource(StartupTimeout);
        IDistributedApplicationTestingBuilder builder = await CreateBuilderAsync(randomizePorts: true, timeout.Token);
        await using DistributedApplication app = await builder.BuildAsync(timeout.Token);
        await app.StartAsync(timeout.Token);
        await app.ResourceNotifications.WaitForResourceHealthyAsync("api", timeout.Token);

        Uri apiAddress = app.GetEndpoint("api", "https");
        var cookies = new CookieContainer();
        using var handler = CreateBrowserHandler(cookies);
        using var client = new HttpClient(handler) { BaseAddress = apiAddress };
        string csrfToken = await SignInAsync(client, cookies, "alice", "topology-user-password", timeout.Token);
        Guid membershipId = await CreateOrganizationAndReadMembershipIdAsync(
            client, csrfToken, "Sales Customers", "sales-customers", timeout.Token);
        using HttpResponseMessage roles = await SendCommandAsync(client, HttpMethod.Put,
            $"/api/o/sales-customers/access/members/{membershipId:D}/roles", csrfToken,
            new { roleIds = AdministratorAndSalesClerkRoleIds }, timeout.Token);
        roles.EnsureSuccessStatusCode();

        const string customersUrl = "/api/o/sales-customers/sales/customers";
        using HttpResponseMessage created = await SendCommandAsync(client, HttpMethod.Post, customersUrl, csrfToken,
            new { code = "north-trading", name = "North Trading" }, timeout.Token);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal("/api/o/sales-customers/sales/customers/NORTH-TRADING", created.Headers.Location?.OriginalString);
        using JsonDocument createdJson = JsonDocument.Parse(await created.Content.ReadAsStringAsync(timeout.Token));
        Guid customerId = createdJson.RootElement.GetProperty("customerId").GetGuid();
        using HttpResponseMessage found = await client.GetAsync(customersUrl + "/north-trading", timeout.Token);
        found.EnsureSuccessStatusCode();
        using JsonDocument foundJson = JsonDocument.Parse(await found.Content.ReadAsStringAsync(timeout.Token));
        Assert.Equal(customerId, foundJson.RootElement.GetProperty("customerId").GetGuid());
        Assert.Equal("NORTH-TRADING", foundJson.RootElement.GetProperty("code").GetString());
        Assert.Equal("North Trading", foundJson.RootElement.GetProperty("name").GetString());

        using HttpResponseMessage duplicate = await SendCommandAsync(client, HttpMethod.Post, customersUrl, csrfToken,
            new { code = " NORTH-TRADING ", name = "Duplicate" }, timeout.Token);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal("application/problem+json", duplicate.Content.Headers.ContentType?.MediaType);
        using HttpResponseMessage invalid = await SendCommandAsync(client, HttpMethod.Post, customersUrl, csrfToken,
            new { code = "another", name = "" }, timeout.Token);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        using JsonDocument invalidJson = JsonDocument.Parse(await invalid.Content.ReadAsStringAsync(timeout.Token));
        Assert.Equal("name", invalidJson.RootElement.GetProperty("field").GetString());
        using HttpResponseMessage noCsrf = await client.PostAsJsonAsync(customersUrl,
            new { code = "no-csrf", name = "Rejected" }, timeout.Token);
        Assert.Equal(HttpStatusCode.BadRequest, noCsrf.StatusCode);
        using HttpResponseMessage noCustomer = await client.GetAsync(customersUrl + "/no-csrf", timeout.Token);
        Assert.Equal(HttpStatusCode.NotFound, noCustomer.StatusCode);

        Guid otherMembership = await CreateOrganizationAndReadMembershipIdAsync(
            client, csrfToken, "Other Sales Customers", "other-sales-customers", timeout.Token);
        using HttpResponseMessage otherRoles = await SendCommandAsync(client, HttpMethod.Put,
            $"/api/o/other-sales-customers/access/members/{otherMembership:D}/roles", csrfToken,
            new { roleIds = AdministratorAndSalesClerkRoleIds }, timeout.Token);
        otherRoles.EnsureSuccessStatusCode();
        using HttpResponseMessage foreign = await client.GetAsync(
            "/api/o/other-sales-customers/sales/customers/north-trading", timeout.Token);
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);

        using HttpResponseMessage revoke = await SendCommandAsync(client, HttpMethod.Put,
            $"/api/o/sales-customers/access/members/{membershipId:D}/roles", csrfToken,
            new { roleIds = SalesTestAdministratorRoleIds }, timeout.Token);
        revoke.EnsureSuccessStatusCode();
        using HttpResponseMessage deniedRead = await client.GetAsync(customersUrl + "/north-trading", timeout.Token);
        Assert.Equal(HttpStatusCode.Forbidden, deniedRead.StatusCode);
        using HttpResponseMessage deniedCreate = await SendCommandAsync(client, HttpMethod.Post, customersUrl, csrfToken,
            new { code = "denied", name = "Denied" }, timeout.Token);
        Assert.Equal(HttpStatusCode.Forbidden, deniedCreate.StatusCode);
    }
}
