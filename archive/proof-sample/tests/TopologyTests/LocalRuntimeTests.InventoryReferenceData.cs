using System.Net;
using System.Text.Json;
using Aspire.Hosting;
using Aspire.Hosting.Testing;

namespace ModulithFoundry.TopologyTests;

public sealed partial class LocalRuntimeTests
{
    private static readonly string[] AdministratorAndInventoryManagerRoleIds =
    [
        "organization-administrator",
        "inventory-manager",
    ];

    [Fact]
    public async Task InventoryReferenceData_AuthenticatedManager_ManagesReferenceData()
    {
        using var timeout = new CancellationTokenSource(StartupTimeout);
        IDistributedApplicationTestingBuilder builder = await CreateBuilderAsync(
            randomizePorts: true,
            timeout.Token
        );
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
            timeout.Token
        );
        Guid membershipId = await CreateOrganizationAndReadMembershipIdAsync(
            client,
            csrfToken,
            "Inventory Reference Data Organization",
            "inventory-catalog",
            timeout.Token
        );
        using HttpResponseMessage roles = await SendCommandAsync(
            client,
            HttpMethod.Put,
            $"/api/o/inventory-catalog/access/members/{membershipId:D}/roles",
            csrfToken,
            new { roleIds = AdministratorAndInventoryManagerRoleIds },
            timeout.Token
        );
        roles.EnsureSuccessStatusCode();

        using HttpResponseMessage item = await SendCommandAsync(
            client,
            HttpMethod.Post,
            "/api/o/inventory-catalog/inventory/items",
            csrfToken,
            new
            {
                sku = "bolt-01",
                description = "Zinc-plated bolt",
                baseUnitCode = "ea",
            },
            timeout.Token
        );
        Assert.Equal(HttpStatusCode.Created, item.StatusCode);
        using JsonDocument itemJson = JsonDocument.Parse(
            await item.Content.ReadAsStringAsync(timeout.Token)
        );
        Assert.Equal("BOLT-01", itemJson.RootElement.GetProperty("sku").GetString());
        Assert.Equal("EA", itemJson.RootElement.GetProperty("baseUnitCode").GetString());

        using HttpResponseMessage location = await SendCommandAsync(
            client,
            HttpMethod.Post,
            "/api/o/inventory-catalog/inventory/locations",
            csrfToken,
            new { code = "main", name = "Main warehouse" },
            timeout.Token
        );
        Assert.Equal(HttpStatusCode.Created, location.StatusCode);

        using HttpResponseMessage receipt = await SendCommandAsync(
            client,
            HttpMethod.Post,
            "/api/o/inventory-catalog/inventory/stock-positions/main/bolt-01/receipts",
            csrfToken,
            new { quantity = 12.5m, expectedVersion = 0 },
            timeout.Token
        );
        receipt.EnsureSuccessStatusCode();
        using JsonDocument receiptJson = JsonDocument.Parse(
            await receipt.Content.ReadAsStringAsync(timeout.Token)
        );
        Assert.Equal(2, receiptJson.RootElement.GetProperty("version").GetInt64());
        Assert.Equal(12.5m, receiptJson.RootElement.GetProperty("onHandQuantity").GetDecimal());

        using HttpResponseMessage position = await client.GetAsync(
            "/api/o/inventory-catalog/inventory/stock-positions/main/bolt-01",
            timeout.Token
        );
        position.EnsureSuccessStatusCode();
        using JsonDocument positionJson = JsonDocument.Parse(
            await position.Content.ReadAsStringAsync(timeout.Token)
        );
        Assert.Equal(
            receiptJson.RootElement.GetProperty("stockPositionId").GetGuid(),
            positionJson.RootElement.GetProperty("stockPositionId").GetGuid()
        );
        Assert.Equal(2, positionJson.RootElement.GetProperty("version").GetInt64());
        Assert.Equal(12.5m, positionJson.RootElement.GetProperty("onHandQuantity").GetDecimal());

        using HttpResponseMessage historical = await client.GetAsync(
            "/api/o/inventory-catalog/inventory/stock-positions/main/bolt-01?version=1",
            timeout.Token
        );
        historical.EnsureSuccessStatusCode();
        using JsonDocument historicalJson = JsonDocument.Parse(
            await historical.Content.ReadAsStringAsync(timeout.Token)
        );
        Assert.Equal(1, historicalJson.RootElement.GetProperty("version").GetInt64());
        Assert.Equal(0m, historicalJson.RootElement.GetProperty("onHandQuantity").GetDecimal());

        using HttpResponseMessage history = await client.GetAsync(
            "/api/o/inventory-catalog/inventory/stock-positions/main/bolt-01/history?limit=1",
            timeout.Token
        );
        history.EnsureSuccessStatusCode();
        string historyBody = await history.Content.ReadAsStringAsync(timeout.Token);
        using JsonDocument historyJson = JsonDocument.Parse(historyBody);
        JsonElement firstEntry = Assert.Single(
            historyJson.RootElement.GetProperty("entries").EnumerateArray()
        );
        Assert.Equal("opened", firstEntry.GetProperty("action").GetString());
        Assert.Equal(1, historyJson.RootElement.GetProperty("nextAfterVersion").GetInt64());
        Assert.DoesNotContain("payload", historyBody, StringComparison.Ordinal);
        Assert.DoesNotContain("metadata", historyBody, StringComparison.Ordinal);
        Assert.DoesNotContain("schemaVersion", historyBody, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "inventory.stock-position.opened",
            historyBody,
            StringComparison.Ordinal
        );

        using HttpResponseMessage historyTail = await client.GetAsync(
            "/api/o/inventory-catalog/inventory/stock-positions/main/bolt-01/history?afterVersion=1&limit=1",
            timeout.Token
        );
        historyTail.EnsureSuccessStatusCode();
        using JsonDocument tailJson = JsonDocument.Parse(
            await historyTail.Content.ReadAsStringAsync(timeout.Token)
        );
        JsonElement lastEntry = Assert.Single(
            tailJson.RootElement.GetProperty("entries").EnumerateArray()
        );
        Assert.Equal("received", lastEntry.GetProperty("action").GetString());
        Assert.Equal(12.5m, lastEntry.GetProperty("quantity").GetDecimal());
        Assert.Equal(
            JsonValueKind.Null,
            tailJson.RootElement.GetProperty("nextAfterVersion").ValueKind
        );

        string recordedAt = Uri.EscapeDataString(firstEntry.GetProperty("recordedAt").GetString()!);
        using HttpResponseMessage asOf = await client.GetAsync(
            $"/api/o/inventory-catalog/inventory/stock-positions/main/bolt-01?recordedAt={recordedAt}",
            timeout.Token
        );
        asOf.EnsureSuccessStatusCode();
        using JsonDocument asOfJson = JsonDocument.Parse(
            await asOf.Content.ReadAsStringAsync(timeout.Token)
        );
        Assert.Equal(2, asOfJson.RootElement.GetProperty("version").GetInt64());
        Assert.Equal(12.5m, asOfJson.RootElement.GetProperty("onHandQuantity").GetDecimal());

        using HttpResponseMessage ambiguous = await client.GetAsync(
            $"/api/o/inventory-catalog/inventory/stock-positions/main/bolt-01?version=1&recordedAt={recordedAt}",
            timeout.Token
        );
        await AssertProblemAsync(ambiguous, HttpStatusCode.BadRequest, timeout.Token);
        using HttpResponseMessage oversized = await client.GetAsync(
            "/api/o/inventory-catalog/inventory/stock-positions/main/bolt-01/history?limit=101",
            timeout.Token
        );
        await AssertProblemAsync(oversized, HttpStatusCode.BadRequest, timeout.Token);

        using HttpResponseMessage staleReceipt = await SendCommandAsync(
            client,
            HttpMethod.Post,
            "/api/o/inventory-catalog/inventory/stock-positions/main/bolt-01/receipts",
            csrfToken,
            new { quantity = 1m, expectedVersion = 0 },
            timeout.Token
        );
        await AssertProblemAsync(staleReceipt, HttpStatusCode.Conflict, timeout.Token);

        using HttpResponseMessage correction = await SendCommandAsync(
            client,
            HttpMethod.Post,
            "/api/o/inventory-catalog/inventory/stock-positions/main/bolt-01/corrections",
            csrfToken,
            new
            {
                onHandQuantity = 9m,
                reason = "Count confirmed",
                expectedVersion = 2,
            },
            timeout.Token
        );
        correction.EnsureSuccessStatusCode();
        using JsonDocument correctionJson = JsonDocument.Parse(
            await correction.Content.ReadAsStringAsync(timeout.Token)
        );
        Assert.Equal(9m, correctionJson.RootElement.GetProperty("onHandQuantity").GetDecimal());
        Assert.Equal(3, correctionJson.RootElement.GetProperty("version").GetInt64());
        using HttpResponseMessage correctionHistory = await client.GetAsync(
            "/api/o/inventory-catalog/inventory/stock-positions/main/bolt-01/history?afterVersion=2",
            timeout.Token
        );
        correctionHistory.EnsureSuccessStatusCode();
        using JsonDocument correctionHistoryJson = JsonDocument.Parse(
            await correctionHistory.Content.ReadAsStringAsync(timeout.Token)
        );
        JsonElement correctionEntry = Assert.Single(
            correctionHistoryJson.RootElement.GetProperty("entries").EnumerateArray()
        );
        Assert.Equal("quantity-corrected", correctionEntry.GetProperty("action").GetString());
        Assert.Equal(9m, correctionEntry.GetProperty("quantity").GetDecimal());
        Assert.Equal("Count confirmed", correctionEntry.GetProperty("reason").GetString());

        using HttpResponseMessage missingCsrf = await SendCommandAsync(
            client,
            HttpMethod.Post,
            "/api/o/inventory-catalog/inventory/stock-positions/main/bolt-01/corrections",
            string.Empty,
            new
            {
                onHandQuantity = 8m,
                reason = "Count confirmed",
                expectedVersion = 3,
            },
            timeout.Token
        );
        await AssertProblemAsync(missingCsrf, HttpStatusCode.BadRequest, timeout.Token);
        using HttpResponseMessage invalidCorrection = await SendCommandAsync(
            client,
            HttpMethod.Post,
            "/api/o/inventory-catalog/inventory/stock-positions/main/bolt-01/corrections",
            csrfToken,
            new
            {
                onHandQuantity = 8m,
                reason = " ",
                expectedVersion = 3,
            },
            timeout.Token
        );
        await AssertProblemAsync(invalidCorrection, HttpStatusCode.BadRequest, timeout.Token);
        using HttpResponseMessage staleCorrection = await SendCommandAsync(
            client,
            HttpMethod.Post,
            "/api/o/inventory-catalog/inventory/stock-positions/main/bolt-01/corrections",
            csrfToken,
            new
            {
                onHandQuantity = 8m,
                reason = "Count confirmed",
                expectedVersion = 2,
            },
            timeout.Token
        );
        await AssertProblemAsync(staleCorrection, HttpStatusCode.Conflict, timeout.Token);

        using HttpResponseMessage items = await client.GetAsync(
            "/api/o/inventory-catalog/inventory/items",
            timeout.Token
        );
        items.EnsureSuccessStatusCode();
        using JsonDocument itemsJson = JsonDocument.Parse(
            await items.Content.ReadAsStringAsync(timeout.Token)
        );
        Assert.Equal(
            "BOLT-01",
            Assert.Single(itemsJson.RootElement.EnumerateArray()).GetProperty("sku").GetString()
        );

        using HttpResponseMessage deactivated = await SendCommandAsync(
            client,
            HttpMethod.Post,
            "/api/o/inventory-catalog/inventory/items/bolt-01/deactivate",
            csrfToken,
            body: null,
            timeout.Token
        );
        deactivated.EnsureSuccessStatusCode();
        using JsonDocument deactivatedJson = JsonDocument.Parse(
            await deactivated.Content.ReadAsStringAsync(timeout.Token)
        );
        Assert.False(deactivatedJson.RootElement.GetProperty("isActive").GetBoolean());

        await CreateOrganizationAndReadMembershipIdAsync(
            client,
            csrfToken,
            "Inventory Denied Organization",
            "inventory-denied",
            timeout.Token
        );
        using HttpResponseMessage denied = await SendCommandAsync(
            client,
            HttpMethod.Post,
            "/api/o/inventory-denied/inventory/items",
            csrfToken,
            new
            {
                sku = "denied-01",
                description = "Denied item",
                baseUnitCode = "EA",
            },
            timeout.Token
        );
        await AssertProblemAsync(denied, HttpStatusCode.Forbidden, timeout.Token);
        using HttpResponseMessage deniedHistory = await client.GetAsync(
            "/api/o/inventory-denied/inventory/stock-positions/main/bolt-01/history",
            timeout.Token
        );
        await AssertProblemAsync(deniedHistory, HttpStatusCode.Forbidden, timeout.Token);
        using HttpResponseMessage deniedCorrection = await SendCommandAsync(
            client,
            HttpMethod.Post,
            "/api/o/inventory-denied/inventory/stock-positions/main/bolt-01/corrections",
            csrfToken,
            new
            {
                onHandQuantity = 8m,
                reason = "Count confirmed",
                expectedVersion = 3,
            },
            timeout.Token
        );
        await AssertProblemAsync(deniedCorrection, HttpStatusCode.Forbidden, timeout.Token);
    }
}
