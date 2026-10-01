using System.Net;
using System.Text.Json;
using Aspire.Hosting;
using Aspire.Hosting.Testing;

namespace ModulithFoundry.TopologyTests;

public sealed partial class LocalRuntimeTests
{
    private static readonly string[] DraftOrderSetupRoleIds =
    [
        "organization-administrator",
        "sales-clerk",
        "inventory-manager",
    ];

    [Fact]
    public async Task SalesOrders_AuthenticatedClerk_CreatesDraftAndRetainsItemSnapshot()
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
            "Draft Orders",
            "draft-orders",
            timeout.Token
        );
        using HttpResponseMessage roles = await SendCommandAsync(
            client,
            HttpMethod.Put,
            $"/api/o/draft-orders/access/members/{membershipId:D}/roles",
            csrf,
            new { roleIds = DraftOrderSetupRoleIds },
            timeout.Token
        );
        roles.EnsureSuccessStatusCode();
        const string salesUrl = "/api/o/draft-orders/sales";
        using HttpResponseMessage customer = await SendCommandAsync(
            client,
            HttpMethod.Post,
            salesUrl + "/customers",
            csrf,
            new { code = "buyer", name = "Buyer" },
            timeout.Token
        );
        customer.EnsureSuccessStatusCode();
        using HttpResponseMessage item = await SendCommandAsync(
            client,
            HttpMethod.Post,
            "/api/o/draft-orders/inventory/items",
            csrf,
            new
            {
                sku = "bolt",
                description = "Original bolt",
                baseUnitCode = "ea",
            },
            timeout.Token
        );
        item.EnsureSuccessStatusCode();
        using JsonDocument itemJson = JsonDocument.Parse(
            await item.Content.ReadAsStringAsync(timeout.Token)
        );
        Guid stockItemId = itemJson.RootElement.GetProperty("stockItemId").GetGuid();
        using HttpResponseMessage created = await SendCommandAsync(
            client,
            HttpMethod.Post,
            salesUrl + "/orders",
            csrf,
            new
            {
                customerCode = "buyer",
                currency = "usd",
                lines = new[]
                {
                    new
                    {
                        stockItemId,
                        quantity = 3.5m,
                        unitPrice = 2.3456m,
                    },
                },
            },
            timeout.Token
        );
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(salesUrl + "/orders/1", created.Headers.Location?.OriginalString);
        using JsonDocument createdJson = JsonDocument.Parse(
            await created.Content.ReadAsStringAsync(timeout.Token)
        );
        Assert.Equal(8.21m, createdJson.RootElement.GetProperty("totalAmount").GetDecimal());
        Assert.Equal("USD", createdJson.RootElement.GetProperty("currency").GetString());
        Assert.Equal(
            stockItemId,
            createdJson.RootElement.GetProperty("lines")[0].GetProperty("stockItemId").GetGuid()
        );
        using HttpResponseMessage change = await SendCommandAsync(
            client,
            HttpMethod.Put,
            "/api/o/draft-orders/inventory/items/bolt/description",
            csrf,
            new { description = "Changed bolt" },
            timeout.Token
        );
        change.EnsureSuccessStatusCode();
        using HttpResponseMessage found = await client.GetAsync(
            salesUrl + "/orders/1",
            timeout.Token
        );
        found.EnsureSuccessStatusCode();
        using JsonDocument foundJson = JsonDocument.Parse(
            await found.Content.ReadAsStringAsync(timeout.Token)
        );
        Assert.Equal(
            "Original bolt",
            foundJson.RootElement.GetProperty("lines")[0].GetProperty("description").GetString()
        );

        using HttpResponseMessage invalid = await SendCommandAsync(
            client,
            HttpMethod.Post,
            salesUrl + "/orders",
            csrf,
            new
            {
                customerCode = "buyer",
                currency = "USD",
                lines = new[]
                {
                    new
                    {
                        stockItemId,
                        quantity = 0m,
                        unitPrice = 1m,
                    },
                },
            },
            timeout.Token
        );
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        using JsonDocument invalidJson = JsonDocument.Parse(
            await invalid.Content.ReadAsStringAsync(timeout.Token)
        );
        Assert.Equal("quantity", invalidJson.RootElement.GetProperty("field").GetString());
        using HttpResponseMessage missing = await SendCommandAsync(
            client,
            HttpMethod.Post,
            salesUrl + "/orders",
            csrf,
            new
            {
                customerCode = "missing",
                currency = "USD",
                lines = new[]
                {
                    new
                    {
                        stockItemId,
                        quantity = 1m,
                        unitPrice = 1m,
                    },
                },
            },
            timeout.Token
        );
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        using HttpResponseMessage unavailable = await SendCommandAsync(
            client,
            HttpMethod.Post,
            salesUrl + "/orders",
            csrf,
            new
            {
                customerCode = "buyer",
                currency = "USD",
                lines = new[]
                {
                    new
                    {
                        stockItemId = Guid.CreateVersion7(),
                        quantity = 1m,
                        unitPrice = 1m,
                    },
                },
            },
            timeout.Token
        );
        Assert.Equal(HttpStatusCode.Conflict, unavailable.StatusCode);
        using HttpResponseMessage revoke = await SendCommandAsync(
            client,
            HttpMethod.Put,
            $"/api/o/draft-orders/access/members/{membershipId:D}/roles",
            csrf,
            new { roleIds = SalesTestAdministratorRoleIds },
            timeout.Token
        );
        revoke.EnsureSuccessStatusCode();
        using HttpResponseMessage denied = await client.GetAsync(
            salesUrl + "/orders/1",
            timeout.Token
        );
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }
}
