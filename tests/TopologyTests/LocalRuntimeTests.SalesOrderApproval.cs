using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;

namespace ModulithFoundry.TopologyTests;

public sealed partial class LocalRuntimeTests
{
    private static readonly string[] ApprovalSetupRoleIds =
    [
        "organization-administrator",
        "sales-manager",
        "sales-clerk",
        "sales-approver",
        "inventory-manager",
        "purchasing-agent",
    ];
    private static readonly string[] ApprovalMemberRoleIds = ["sales-approver"];
    private static readonly string[] NonApproverRoleIds = ["sales-clerk"];

    [Theory]
    [InlineData(1, null)]
    [InlineData(2, null)]
    [InlineData(2, false)]
    [InlineData(2, true)]
    public async Task SalesOrderApproval_IndependentAuthenticatedMember_CompletesReservationRoundTrip(
        int replicas,
        bool? cancellationAfterCommit
    )
    {
        using var timeout = new CancellationTokenSource(
            cancellationAfterCommit is null
                ? StartupTimeout
                : StartupTimeout + TimeSpan.FromMinutes(1)
        );
        await using var telemetry = await OtlpTestReceiver.StartAsync(timeout.Token);
        IDistributedApplicationTestingBuilder builder = await CreateBuilderAsync(
            randomizePorts: true,
            timeout.Token
        );
        if (cancellationAfterCommit is not null)
            builder
                .CreateResourceBuilder<PostgresServerResource>("postgres")
                .WithArgs("-c", "client_connection_check_interval=100ms");
        builder
            .CreateResourceBuilder<ProjectResource>("api")
            .WithReplicas(replicas)
            .WithEnvironment("OTEL_EXPORTER_OTLP_ENDPOINT", telemetry.Endpoint.ToString())
            .WithEnvironment("OTEL_EXPORTER_OTLP_PROTOCOL", "http/protobuf");
        await using DistributedApplication app = await builder.BuildAsync(timeout.Token);
        await app.StartAsync(timeout.Token);
        await app.ResourceNotifications.WaitForResourceHealthyAsync("api", timeout.Token);
        string[] replicaIds = await WaitForApiReplicasAsync(app, replicas, timeout.Token);
        Uri address = app.GetEndpoint("api", "https");
        var aliceCookies = new CookieContainer();
        using var aliceHandler = CreateBrowserHandler(aliceCookies);
        using var alice = new HttpClient(aliceHandler) { BaseAddress = address };
        string aliceCsrf = await SignInAsync(
            alice,
            aliceCookies,
            "alice",
            "topology-user-password",
            timeout.Token
        );
        Guid aliceMembership = await CreateOrganizationAndReadMembershipIdAsync(
            alice,
            aliceCsrf,
            "Order Approval",
            "order-approval",
            timeout.Token
        );
        const string root = "/api/o/order-approval";
        using HttpResponseMessage aliceRoles = await SendCommandAsync(
            alice,
            HttpMethod.Put,
            $"{root}/access/members/{aliceMembership:D}/roles",
            aliceCsrf,
            new { roleIds = ApprovalSetupRoleIds },
            timeout.Token
        );
        aliceRoles.EnsureSuccessStatusCode();
        using HttpResponseMessage invitation = await SendCommandAsync(
            alice,
            HttpMethod.Post,
            root + "/invitations",
            aliceCsrf,
            new { recipientEmail = "bob@example.test", roleIds = ApprovalMemberRoleIds },
            timeout.Token
        );
        invitation.EnsureSuccessStatusCode();
        using HttpClient mailpit = app.CreateHttpClient("mailpit", "http");
        Uri link = await WaitForInvitationLinkAsync(mailpit, "bob@example.test", timeout.Token);
        var bobCookies = new CookieContainer();
        using var bobHandler = CreateBrowserHandler(bobCookies);
        using var bob = new HttpClient(bobHandler) { BaseAddress = address };
        using HttpResponseMessage challenge = await bob.GetAsync(link.PathAndQuery, timeout.Token);
        using HttpResponseMessage login = await GetFollowingRedirectsAsync(
            bob,
            bobCookies,
            Assert.IsType<Uri>(challenge.Headers.Location),
            timeout.Token
        );
        using HttpResponseMessage acceptance = await SubmitKeycloakCredentialsAsync(
            bob,
            bobCookies,
            login,
            "bob",
            "topology-user-password",
            timeout.Token
        );
        Assert.Equal(HttpStatusCode.Redirect, acceptance.StatusCode);
        string bobCsrf = await GetCsrfTokenAsync(bob, timeout.Token);
        using HttpResponseMessage members = await alice.GetAsync(
            root + "/access/members",
            timeout.Token
        );
        members.EnsureSuccessStatusCode();
        using JsonDocument membershipJson = JsonDocument.Parse(
            await members.Content.ReadAsStringAsync(timeout.Token)
        );
        Guid bobMembership = Assert
            .Single(
                membershipJson.RootElement.GetProperty("members").EnumerateArray(),
                member => member.GetProperty("email").GetString() == "bob@example.test"
            )
            .GetProperty("membershipId")
            .GetGuid();
        using HttpResponseMessage authority = await SendCommandAsync(
            alice,
            HttpMethod.Put,
            $"{root}/sales/approval-authorities/{bobMembership:D}",
            aliceCsrf,
            new
            {
                maximumAmount = 50m,
                currency = "USD",
                expectedVersion = 0,
            },
            timeout.Token
        );
        authority.EnsureSuccessStatusCode();
        using HttpResponseMessage customer = await SendCommandAsync(
            alice,
            HttpMethod.Post,
            root + "/sales/customers",
            aliceCsrf,
            new { code = "buyer", name = "Buyer" },
            timeout.Token
        );
        customer.EnsureSuccessStatusCode();
        using HttpResponseMessage item = await SendCommandAsync(
            alice,
            HttpMethod.Post,
            root + "/inventory/items",
            aliceCsrf,
            new
            {
                sku = "bolt",
                description = "Bolt",
                baseUnitCode = "ea",
            },
            timeout.Token
        );
        item.EnsureSuccessStatusCode();
        using JsonDocument itemJson = JsonDocument.Parse(
            await item.Content.ReadAsStringAsync(timeout.Token)
        );
        Guid stockItemId = itemJson.RootElement.GetProperty("stockItemId").GetGuid();
        using HttpResponseMessage location = await SendCommandAsync(
            alice,
            HttpMethod.Post,
            root + "/inventory/locations",
            aliceCsrf,
            new { code = "main", name = "Main" },
            timeout.Token
        );
        location.EnsureSuccessStatusCode();
        using HttpResponseMessage receipt = await SendCommandAsync(
            alice,
            HttpMethod.Post,
            root + "/inventory/stock-positions/main/bolt/receipts",
            aliceCsrf,
            new { quantity = 10m, expectedVersion = 0 },
            timeout.Token
        );
        receipt.EnsureSuccessStatusCode();
        using HttpResponseMessage draft = await SendCommandAsync(
            alice,
            HttpMethod.Post,
            root + "/sales/orders",
            aliceCsrf,
            new
            {
                customerCode = "buyer",
                currency = "USD",
                lines = new[]
                {
                    new
                    {
                        stockItemId,
                        quantity = 2m,
                        unitPrice = 25m,
                    },
                },
            },
            timeout.Token
        );
        draft.EnsureSuccessStatusCode();
        using JsonDocument draftJson = JsonDocument.Parse(
            await draft.Content.ReadAsStringAsync(timeout.Token)
        );
        long number = draftJson.RootElement.GetProperty("orderNumber").GetInt64();
        string orderUrl = $"{root}/sales/orders/{number}";
        using HttpResponseMessage submitted = await SendCommandAsync(
            alice,
            HttpMethod.Post,
            orderUrl + "/submit",
            aliceCsrf,
            new { expectedVersion = 1 },
            timeout.Token
        );
        submitted.EnsureSuccessStatusCode();
        using HttpResponseMessage self = await SendCommandAsync(
            alice,
            HttpMethod.Post,
            orderUrl + "/approve",
            aliceCsrf,
            new { expectedVersion = 2 },
            timeout.Token
        );
        Assert.Equal(HttpStatusCode.Forbidden, self.StatusCode);
        using HttpResponseMessage noCsrf = await bob.PostAsJsonAsync(
            orderUrl + "/approve",
            new { expectedVersion = 2 },
            timeout.Token
        );
        Assert.Equal(HttpStatusCode.BadRequest, noCsrf.StatusCode);
        using HttpResponseMessage invalid = await SendCommandAsync(
            bob,
            HttpMethod.Post,
            orderUrl + "/approve",
            bobCsrf,
            new { expectedVersion = 0 },
            timeout.Token
        );
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        using HttpResponseMessage approved = await SendCommandAsync(
            bob,
            HttpMethod.Post,
            orderUrl + "/approve",
            bobCsrf,
            new { expectedVersion = 2 },
            timeout.Token
        );
        approved.EnsureSuccessStatusCode();
        using JsonDocument approvedJson = JsonDocument.Parse(
            await approved.Content.ReadAsStringAsync(timeout.Token)
        );
        Assert.Equal("approved", approvedJson.RootElement.GetProperty("status").GetString());
        Assert.Equal(3, approvedJson.RootElement.GetProperty("version").GetInt64());
        Assert.NotEqual(
            approvedJson.RootElement.GetProperty("submittedBy").GetGuid(),
            approvedJson.RootElement.GetProperty("approvedBy").GetGuid()
        );
        using var reservationTimeout = CancellationTokenSource.CreateLinkedTokenSource(
            timeout.Token
        );
        reservationTimeout.CancelAfter(TimeSpan.FromSeconds(30));
        while (true)
        {
            using HttpResponseMessage process = await bob.GetAsync(
                orderUrl + "/fulfilment",
                reservationTimeout.Token
            );
            process.EnsureSuccessStatusCode();
            using JsonDocument processJson = JsonDocument.Parse(
                await process.Content.ReadAsStringAsync(reservationTimeout.Token)
            );
            if (processJson.RootElement.GetProperty("status").GetString() == "reserved")
            {
                Assert.Equal(number, processJson.RootElement.GetProperty("orderNumber").GetInt64());
                Assert.Equal(
                    "reserved",
                    Assert
                        .Single(processJson.RootElement.GetProperty("lines").EnumerateArray())
                        .GetProperty("status")
                        .GetString()
                );
                break;
            }
            await Task.Delay(100, reservationTimeout.Token);
        }
        using HttpResponseMessage stock = await alice.GetAsync(
            root + "/inventory/stock-positions/main/bolt",
            timeout.Token
        );
        stock.EnsureSuccessStatusCode();
        using JsonDocument stockJson = JsonDocument.Parse(
            await stock.Content.ReadAsStringAsync(timeout.Token)
        );
        Assert.Equal(2m, stockJson.RootElement.GetProperty("reservedQuantity").GetDecimal());
        using HttpResponseMessage activity = await bob.GetAsync(
            orderUrl + "/activity",
            timeout.Token
        );
        activity.EnsureSuccessStatusCode();
        using JsonDocument activityJson = JsonDocument.Parse(
            await activity.Content.ReadAsStringAsync(timeout.Token)
        );
        Assert.Equal(
            "stock-reserved",
            activityJson.RootElement.EnumerateArray().Last().GetProperty("kind").GetString()
        );
        if (replicas == 2 && cancellationAfterCommit is null)
        {
            ExecuteCommandResult stopped = await app.ResourceCommands.ExecuteCommandAsync(
                replicaIds[0],
                KnownResourceCommands.StopCommand,
                timeout.Token
            );
            Assert.True(stopped.Success, stopped.Message);
            await WaitForApiReplicaExitAsync(app, replicaIds[0], timeout.Token);
            // The same authenticated cookies/CSRF and committed workflow now use the survivor.
            using HttpResponseMessage session = await alice.GetAsync("/api/session", timeout.Token);
            Assert.Equal(HttpStatusCode.OK, session.StatusCode);
        }
        await AssertShortageReplenishmentAsync(
            alice,
            bob,
            aliceCsrf,
            bobCsrf,
            root,
            stockItemId,
            timeout.Token
        );
        await AssertCancellationAsync(
            alice,
            bob,
            aliceCsrf,
            bobCsrf,
            root,
            orderUrl,
            timeout.Token,
            cancellationAfterCommit is { } afterCommit
                ? () =>
                    CancelAcrossApiCrashAsync(
                        app,
                        alice,
                        aliceCsrf,
                        orderUrl,
                        replicaIds,
                        afterCommit,
                        timeout.Token
                    )
                : null
        );
        using HttpResponseMessage retry = await SendCommandAsync(
            bob,
            HttpMethod.Post,
            orderUrl + "/approve",
            bobCsrf,
            new { expectedVersion = 2 },
            timeout.Token
        );
        Assert.Equal(HttpStatusCode.Conflict, retry.StatusCode);
        Assert.Equal("application/problem+json", retry.Content.Headers.ContentType?.MediaType);
        using HttpResponseMessage revokedRoles = await SendCommandAsync(
            alice,
            HttpMethod.Put,
            $"{root}/access/members/{bobMembership:D}/roles",
            aliceCsrf,
            new { roleIds = NonApproverRoleIds },
            timeout.Token
        );
        revokedRoles.EnsureSuccessStatusCode();
        using HttpResponseMessage revoked = await SendCommandAsync(
            bob,
            HttpMethod.Post,
            orderUrl + "/approve",
            bobCsrf,
            new { expectedVersion = 3 },
            timeout.Token
        );
        Assert.Equal(HttpStatusCode.Forbidden, revoked.StatusCode);
    }

    private static async Task AssertShortageReplenishmentAsync(
        HttpClient alice,
        HttpClient bob,
        string aliceCsrf,
        string bobCsrf,
        string root,
        Guid stockItemId,
        CancellationToken cancellationToken
    )
    {
        using var draft = await SendCommandAsync(
            alice,
            HttpMethod.Post,
            root + "/sales/orders",
            aliceCsrf,
            new
            {
                customerCode = "buyer",
                currency = "USD",
                lines = new[]
                {
                    new
                    {
                        stockItemId,
                        quantity = 20m,
                        unitPrice = 1m,
                    },
                },
            },
            cancellationToken
        );
        draft.EnsureSuccessStatusCode();
        using var draftJson = JsonDocument.Parse(
            await draft.Content.ReadAsStringAsync(cancellationToken)
        );
        long number = draftJson.RootElement.GetProperty("orderNumber").GetInt64();
        string orderUrl = root + $"/sales/orders/{number}";
        using var submitted = await SendCommandAsync(
            alice,
            HttpMethod.Post,
            orderUrl + "/submit",
            aliceCsrf,
            new { expectedVersion = 1 },
            cancellationToken
        );
        submitted.EnsureSuccessStatusCode();
        using var approved = await SendCommandAsync(
            bob,
            HttpMethod.Post,
            orderUrl + "/approve",
            bobCsrf,
            new { expectedVersion = 2 },
            cancellationToken
        );
        approved.EnsureSuccessStatusCode();
        using var bound = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        bound.CancelAfter(TimeSpan.FromSeconds(30));
        long requirementNumber;
        Guid requirementId;
        while (true)
        {
            using var response = await bob.GetAsync(orderUrl + "/fulfilment", bound.Token);
            response.EnsureSuccessStatusCode();
            using var process = JsonDocument.Parse(
                await response.Content.ReadAsStringAsync(bound.Token)
            );
            var line = Assert.Single(process.RootElement.GetProperty("lines").EnumerateArray());
            if (line.GetProperty("replenishmentRequirementNumber").ValueKind != JsonValueKind.Null)
            {
                Assert.Equal(
                    "awaiting-replenishment",
                    process.RootElement.GetProperty("status").GetString()
                );
                Assert.Equal("shortage", line.GetProperty("status").GetString());
                Assert.Equal(12m, line.GetProperty("replenishmentQuantity").GetDecimal());
                requirementNumber = line.GetProperty("replenishmentRequirementNumber").GetInt64();
                requirementId = line.GetProperty("replenishmentRequirementId").GetGuid();
                break;
            }
            await Task.Delay(100, bound.Token);
        }
        using var requirement = await alice.GetAsync(
            root + $"/purchasing/requirements/{requirementNumber}",
            cancellationToken
        );
        requirement.EnsureSuccessStatusCode();
        using var requirementJson = JsonDocument.Parse(
            await requirement.Content.ReadAsStringAsync(cancellationToken)
        );
        Assert.Equal(
            requirementId,
            requirementJson.RootElement.GetProperty("requirementId").GetGuid()
        );
        Assert.Equal(12m, requirementJson.RootElement.GetProperty("quantity").GetDecimal());
        Assert.Equal("BOLT", requirementJson.RootElement.GetProperty("sku").GetString());
        using var forbidden = await bob.GetAsync(
            root + $"/purchasing/requirements/{requirementNumber}",
            cancellationToken
        );
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        using var list = await alice.GetAsync(root + "/purchasing/requirements", cancellationToken);
        list.EnsureSuccessStatusCode();
        using var listJson = JsonDocument.Parse(
            await list.Content.ReadAsStringAsync(cancellationToken)
        );
        Assert.Single(listJson.RootElement.EnumerateArray());
        using var activity = await bob.GetAsync(orderUrl + "/activity", cancellationToken);
        activity.EnsureSuccessStatusCode();
        using var activityJson = JsonDocument.Parse(
            await activity.Content.ReadAsStringAsync(cancellationToken)
        );
        Assert.Equal(
            "replenishment-created",
            activityJson.RootElement.EnumerateArray().Last().GetProperty("kind").GetString()
        );
    }
}
