using System.Net;
using System.Text.Json;
using Aspire.Hosting;
using Aspire.Hosting.Testing;

namespace ModulithFoundry.TopologyTests;

public sealed partial class LocalRuntimeTests
{
    private static readonly string[] AdministratorAndInventoryManagerRoleIds =
        ["organization-administrator", "inventory-manager"];

    [Fact]
    public async Task InventoryReferenceData_AuthenticatedManager_ManagesReferenceData()
    {
        using var timeout = new CancellationTokenSource(StartupTimeout);
        IDistributedApplicationTestingBuilder builder = await CreateBuilderAsync(
            randomizePorts: true,
            timeout.Token);
        await using DistributedApplication app = await builder.BuildAsync(timeout.Token);
        await app.StartAsync(timeout.Token);
        await app.ResourceNotifications.WaitForResourceHealthyAsync("api", timeout.Token);

        Uri apiAddress = app.GetEndpoint("api", "https");
        var cookies = new CookieContainer();
        using var handler = CreateBrowserHandler(cookies);
        using var client = new HttpClient(handler) { BaseAddress = apiAddress };
        string csrfToken = await SignInAsync(
            client,
            cookies,
            "alice",
            "topology-user-password",
            timeout.Token);
        Guid membershipId = await CreateOrganizationAndReadMembershipIdAsync(
            client,
            csrfToken,
            "Inventory Reference Data Organization",
            "inventory-catalog",
            timeout.Token);
        using HttpResponseMessage roles = await SendCommandAsync(
            client,
            HttpMethod.Put,
            $"/api/o/inventory-catalog/access/members/{membershipId:D}/roles",
            csrfToken,
            new { roleIds = AdministratorAndInventoryManagerRoleIds },
            timeout.Token);
        roles.EnsureSuccessStatusCode();

        using HttpResponseMessage item = await SendCommandAsync(
            client,
            HttpMethod.Post,
            "/api/o/inventory-catalog/inventory/items",
            csrfToken,
            new { sku = "bolt-01", description = "Zinc-plated bolt", baseUnitCode = "ea" },
            timeout.Token);
        Assert.Equal(HttpStatusCode.Created, item.StatusCode);
        using JsonDocument itemJson = JsonDocument.Parse(
            await item.Content.ReadAsStringAsync(timeout.Token));
        Assert.Equal("BOLT-01", itemJson.RootElement.GetProperty("sku").GetString());
        Assert.Equal("EA", itemJson.RootElement.GetProperty("baseUnitCode").GetString());

        using HttpResponseMessage location = await SendCommandAsync(
            client,
            HttpMethod.Post,
            "/api/o/inventory-catalog/inventory/locations",
            csrfToken,
            new { code = "main", name = "Main warehouse" },
            timeout.Token);
        Assert.Equal(HttpStatusCode.Created, location.StatusCode);

        using HttpResponseMessage items = await client.GetAsync(
            "/api/o/inventory-catalog/inventory/items",
            timeout.Token);
        items.EnsureSuccessStatusCode();
        using JsonDocument itemsJson = JsonDocument.Parse(
            await items.Content.ReadAsStringAsync(timeout.Token));
        Assert.Equal(
            "BOLT-01",
            Assert.Single(itemsJson.RootElement.EnumerateArray()).GetProperty("sku").GetString());

        using HttpResponseMessage deactivated = await SendCommandAsync(
            client,
            HttpMethod.Post,
            "/api/o/inventory-catalog/inventory/items/bolt-01/deactivate",
            csrfToken,
            body: null,
            timeout.Token);
        deactivated.EnsureSuccessStatusCode();
        using JsonDocument deactivatedJson = JsonDocument.Parse(
            await deactivated.Content.ReadAsStringAsync(timeout.Token));
        Assert.False(deactivatedJson.RootElement.GetProperty("isActive").GetBoolean());

        await CreateOrganizationAndReadMembershipIdAsync(
            client,
            csrfToken,
            "Inventory Denied Organization",
            "inventory-denied",
            timeout.Token);
        using HttpResponseMessage denied = await SendCommandAsync(
            client,
            HttpMethod.Post,
            "/api/o/inventory-denied/inventory/items",
            csrfToken,
            new { sku = "denied-01", description = "Denied item", baseUnitCode = "EA" },
            timeout.Token);
        await AssertProblemAsync(denied, HttpStatusCode.Forbidden, timeout.Token);
    }
}
