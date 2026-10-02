using Microsoft.Extensions.DependencyInjection;
using ModulithFoundry.Modules.Access.Contracts;
using ModulithFoundry.Modules.Purchasing.Composition;
using ModulithFoundry.Modules.Purchasing.Contracts;
using Npgsql;
using Testcontainers.PostgreSql;

namespace ModulithFoundry.PersistenceTests;

public sealed class PurchaseOrderPersistenceTests
{
    [Fact]
    public async Task CreatePurchaseOrder_ValidDraft_IsRetrievableInFreshScope()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:18.6").Build();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        var organization = Organization();
        await using var services = await ServicesAsync(
            postgres.GetConnectionString(),
            organization
        );
        PurchaseOrderView created;
        await using (var scope = services.CreateAsyncScope())
        {
            created = Assert
                .IsType<CreatePurchaseOrderResult.Created>(
                    await scope
                        .ServiceProvider.GetRequiredService<IPurchaseOrderDrafting>()
                        .CreateAsync(
                            new(
                                organization.UserId,
                                organization.OrganizationId,
                                " po-100 ",
                                "SUP-1",
                                "EUR"
                            ),
                            TestContext.Current.CancellationToken
                        )
                )
                .Order;
        }
        await using var read = services.CreateAsyncScope();
        var found = Assert
            .IsType<GetPurchaseOrderResult.Found>(
                await read
                    .ServiceProvider.GetRequiredService<IPurchaseOrderQueries>()
                    .GetByCodeAsync(
                        organization.UserId,
                        organization.OrganizationId,
                        "PO-100",
                        TestContext.Current.CancellationToken
                    )
            )
            .Order;
        Assert.Equal("PO-100", found.Code);
        Assert.Equal("draft", found.Status);
        Assert.Equal(1, found.Version);
        Assert.Equal(created.PurchaseOrderId, found.PurchaseOrderId);
        Assert.Empty(found.Lines);
    }

    private static OrganizationAccessContext Organization() =>
        new(
            new UserId(Guid.CreateVersion7()),
            new OrganizationId(Guid.CreateVersion7()),
            new MembershipId(Guid.CreateVersion7()),
            "Purchasing Tests",
            "purchasing-tests",
            [PurchasingRoleIds.Agent]
        );

    [Fact]
    public async Task SetLine_ReplacesExistingQuantity_PreservesOneLineAndAdvancesVersion()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:18.6").Build();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        var organization = Organization();
        await using var services = await ServicesAsync(
            postgres.GetConnectionString(),
            organization
        );
        var order = await CreateAsync(services, organization);
        var first = Assert
            .IsType<SetPurchaseOrderLineResult.Changed>(
                await SetLineAsync(services, organization, order.PurchaseOrderId, 1, 2.5m)
            )
            .Order;
        var changed = Assert
            .IsType<SetPurchaseOrderLineResult.Changed>(
                await SetLineAsync(services, organization, order.PurchaseOrderId, first.Version, 4m)
            )
            .Order;
        Assert.Equal(3, changed.Version);
        Assert.Equal(4m, Assert.Single(changed.Lines).Quantity);
        Assert.IsType<SetPurchaseOrderLineResult.VersionConflict>(
            await SetLineAsync(services, organization, order.PurchaseOrderId, 1, 4m)
        );
    }

    private static async Task<PurchaseOrderView> CreateAsync(
        ServiceProvider services,
        OrganizationAccessContext organization,
        string code = "PO-100"
    )
    {
        await using var scope = services.CreateAsyncScope();
        return Assert
            .IsType<CreatePurchaseOrderResult.Created>(
                await scope
                    .ServiceProvider.GetRequiredService<IPurchaseOrderDrafting>()
                    .CreateAsync(
                        new(organization.UserId, organization.OrganizationId, code, "SUP-1", "EUR"),
                        TestContext.Current.CancellationToken
                    )
            )
            .Order;
    }

    private static async Task<SetPurchaseOrderLineResult> SetLineAsync(
        ServiceProvider services,
        OrganizationAccessContext organization,
        Guid id,
        long version,
        decimal quantity,
        string itemCode = "PART-1",
        decimal unitPrice = 12.50m
    )
    {
        await using var scope = services.CreateAsyncScope();
        return await scope
            .ServiceProvider.GetRequiredService<IPurchaseOrderDrafting>()
            .SetLineAsync(
                new(
                    organization.UserId,
                    organization.OrganizationId,
                    id,
                    version,
                    itemCode,
                    quantity,
                    unitPrice
                ),
                TestContext.Current.CancellationToken
            );
    }

    [Fact]
    public async Task IssuePurchaseOrder_NonemptyDraft_CommitsIndependentSummaryAndHistoricalState()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:18.6").Build();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        var organization = Organization();
        var clock = new TestClock();
        await using var services = await ServicesAsync(
            postgres.GetConnectionString(),
            organization,
            clock
        );
        var order = await CreateAsync(services, organization);
        Assert.IsType<IssuePurchaseOrderResult.Rejected>(
            await IssueAsync(services, organization, order.PurchaseOrderId, 1)
        );
        clock.Now = clock.Now.AddSeconds(1);
        var lined = Assert
            .IsType<SetPurchaseOrderLineResult.Changed>(
                await SetLineAsync(services, organization, order.PurchaseOrderId, 1, 2.5m)
            )
            .Order;
        clock.Now = clock.Now.AddSeconds(1);
        var issued = Assert
            .IsType<IssuePurchaseOrderResult.Issued>(
                await IssueAsync(services, organization, order.PurchaseOrderId, 2)
            )
            .Order;
        Assert.Equal("issued", issued.Status);
        Assert.Equal(3, issued.Version);
        Assert.IsType<SetPurchaseOrderLineResult.Rejected>(
            await SetLineAsync(services, organization, order.PurchaseOrderId, 3, 5m)
        );
        await using var scope = services.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<IPurchaseOrderQueries>();
        var summary = Assert.Single(
            Assert
                .IsType<ListPurchaseOrderSummariesResult.Listed>(
                    await queries.ListSummariesAsync(
                        organization.UserId,
                        organization.OrganizationId,
                        cancellationToken: TestContext.Current.CancellationToken
                    )
                )
                .Orders
        );
        Assert.Equal(31.25m, summary.Total);
        Assert.Equal(1, summary.LineCount);
        Assert.True(summary.IsIssued);
        Assert.Equal(3, summary.Version);
        var live = Assert
            .IsType<GetPurchaseOrderResult.Found>(
                await queries.GetLiveAsync(
                    organization.UserId,
                    organization.OrganizationId,
                    order.PurchaseOrderId,
                    TestContext.Current.CancellationToken
                )
            )
            .Order;
        Assert.Equal("issued", live.Status);
        Assert.Equal(2.5m, Assert.Single(live.Lines).Quantity);
        Assert.Equal(3, live.Version);
        var history = Assert
            .IsType<GetPurchaseOrderResult.Found>(
                await queries.GetAtVersionAsync(
                    organization.UserId,
                    organization.OrganizationId,
                    order.PurchaseOrderId,
                    2,
                    TestContext.Current.CancellationToken
                )
            )
            .Order;
        Assert.Equal("draft", history.Status);
        Assert.Equal(2, history.Version);
        Assert.Equal(2.5m, Assert.Single(history.Lines).Quantity);
        var timed = Assert
            .IsType<GetPurchaseOrderResult.Found>(
                await queries.GetRecordedAtAsync(
                    organization.UserId,
                    organization.OrganizationId,
                    order.PurchaseOrderId,
                    lined.RecordedAt.ToOffset(TimeSpan.FromHours(2)),
                    TestContext.Current.CancellationToken
                )
            )
            .Order;
        Assert.Equal("draft", timed.Status);
        Assert.Equal(2, timed.Version);
        Assert.IsType<GetPurchaseOrderResult.NotFound>(
            await queries.GetAtVersionAsync(
                organization.UserId,
                organization.OrganizationId,
                order.PurchaseOrderId,
                4,
                TestContext.Current.CancellationToken
            )
        );
        Assert.IsType<GetPurchaseOrderResult.NotFound>(
            await queries.GetRecordedAtAsync(
                organization.UserId,
                organization.OrganizationId,
                order.PurchaseOrderId,
                order.RecordedAt.AddSeconds(-1),
                TestContext.Current.CancellationToken
            )
        );
    }

    private static async Task<IssuePurchaseOrderResult> IssueAsync(
        ServiceProvider services,
        OrganizationAccessContext organization,
        Guid id,
        long version
    )
    {
        await using var scope = services.CreateAsyncScope();
        return await scope
            .ServiceProvider.GetRequiredService<IPurchaseOrderIssuance>()
            .IssueAsync(
                new(organization.UserId, organization.OrganizationId, id, version),
                TestContext.Current.CancellationToken
            );
    }

    [Theory]
    [InlineData("purchase_order_summaries", "UPDATE")]
    [InlineData("purchase_order_write_models", "UPDATE")]
    [InlineData("audit_entries", "INSERT")]
    public async Task SetLine_RequiredPersistenceFails_RollsBackHistoryAndBothViews(
        string table,
        string operation
    )
    {
        await using var postgres = new PostgreSqlBuilder("postgres:18.6").Build();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        var organization = Organization();
        await using var services = await ServicesAsync(
            postgres.GetConnectionString(),
            organization
        );
        var order = await CreateAsync(services, organization);
        Assert.IsType<SetPurchaseOrderLineResult.Changed>(
            await SetLineAsync(services, organization, order.PurchaseOrderId, 1, 2.5m)
        );
        await SetupAsync(
            postgres.GetConnectionString(),
            $"""
            CREATE FUNCTION purchasing.reject_purchase_order_change() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN RAISE EXCEPTION 'injected persistence failure'; END; $$;
            CREATE TRIGGER reject_purchase_order_change BEFORE {operation} ON purchasing.{table}
            FOR EACH ROW EXECUTE FUNCTION purchasing.reject_purchase_order_change();
            """
        );
        await Assert.ThrowsAsync<Microsoft.EntityFrameworkCore.DbUpdateException>(() =>
            SetLineAsync(services, organization, order.PurchaseOrderId, 2, 4m)
        );
        await AssertCurrentAsync(services, organization, order.PurchaseOrderId, 2, 2.5m, 31.25m);
        await SetupAsync(
            postgres.GetConnectionString(),
            $"DROP TRIGGER reject_purchase_order_change ON purchasing.{table}"
        );
        Assert.IsType<SetPurchaseOrderLineResult.Changed>(
            await SetLineAsync(services, organization, order.PurchaseOrderId, 2, 4m)
        );
        await AssertCurrentAsync(services, organization, order.PurchaseOrderId, 3, 4m, 50m);
    }

    [Fact]
    public async Task SetLine_CompetingAppends_OneCommitAndOneVersionConflict()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:18.6").Build();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        var organization = Organization();
        var connection = new NpgsqlConnectionStringBuilder(postgres.GetConnectionString())
        {
            ApplicationName = "purchase-order-race",
        }.ConnectionString;
        await using var services = await ServicesAsync(connection, organization);
        var order = await CreateAsync(services, organization);
        await SetupAsync(
            postgres.GetConnectionString(),
            $"""
            CREATE FUNCTION purchasing.pause_purchase_order_append() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                IF NEW.id = '{order.PurchaseOrderId}'::uuid THEN PERFORM pg_advisory_xact_lock(813101); END IF;
                RETURN NEW;
            END; $$;
            CREATE TRIGGER pause_purchase_order_append BEFORE UPDATE ON purchasing.event_streams
            FOR EACH ROW EXECUTE FUNCTION purchasing.pause_purchase_order_append();
            """
        );
        await using var barrier = new NpgsqlConnection(postgres.GetConnectionString());
        await barrier.OpenAsync(TestContext.Current.CancellationToken);
        await using (var hold = new NpgsqlCommand("SELECT pg_advisory_lock(813101)", barrier))
            await hold.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        Task<SetPurchaseOrderLineResult> first = SetLineAsync(
            services,
            organization,
            order.PurchaseOrderId,
            1,
            2m
        );
        Task<SetPurchaseOrderLineResult> second = SetLineAsync(
            services,
            organization,
            order.PurchaseOrderId,
            1,
            4m
        );
        try
        {
            // Coordination only: both actual persistence attempts are blocked, not sequential stale requests.
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(
                TestContext.Current.CancellationToken
            );
            timeout.CancelAfter(TimeSpan.FromSeconds(15));
            while (true)
            {
                await using var waiting = new NpgsqlCommand(
                    """
                    SELECT count(*) FROM pg_stat_activity WHERE application_name = 'purchase-order-race'
                    AND state = 'active' AND wait_event_type = 'Lock'
                    """,
                    barrier
                );
                if (
                    Convert.ToInt64(
                        await waiting.ExecuteScalarAsync(timeout.Token),
                        System.Globalization.CultureInfo.InvariantCulture
                    ) >= 2
                )
                    break;
                await Task.Delay(50, timeout.Token);
            }
        }
        finally
        {
            await using var release = new NpgsqlCommand(
                "SELECT pg_advisory_unlock(813101)",
                barrier
            );
            await release.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }
        var results = await Task.WhenAll(first, second);
        var winner = Assert.Single(results.OfType<SetPurchaseOrderLineResult.Changed>()).Order;
        Assert.Single(results.OfType<SetPurchaseOrderLineResult.VersionConflict>());
        var quantity = Assert.Single(winner.Lines).Quantity;
        Assert.True(quantity is 2m or 4m);
        await AssertCurrentAsync(
            services,
            organization,
            order.PurchaseOrderId,
            2,
            quantity,
            quantity == 2m ? 25m : 50m
        );
    }

    [Fact]
    public async Task PurchaseOrder_OtherOrganizationAndDeniedActor_CannotReadOrChangeOwnedState()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:18.6").Build();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        var firstOrganization = Organization();
        var secondOrganization = Organization();
        await using var first = await ServicesAsync(
            postgres.GetConnectionString(),
            firstOrganization
        );
        await using var second = await ServicesAsync(
            postgres.GetConnectionString(),
            secondOrganization
        );
        var owned = await CreateAsync(first, firstOrganization);
        var other = await CreateAsync(second, secondOrganization);
        Assert.NotEqual(owned.PurchaseOrderId, other.PurchaseOrderId);
        Assert.IsType<SetPurchaseOrderLineResult.NotFound>(
            await SetLineAsync(second, secondOrganization, owned.PurchaseOrderId, 1, 1m)
        );
        await using var read = second.CreateAsyncScope();
        var queries = read.ServiceProvider.GetRequiredService<IPurchaseOrderQueries>();
        Assert.IsType<GetPurchaseOrderResult.NotFound>(
            await queries.GetLiveAsync(
                secondOrganization.UserId,
                secondOrganization.OrganizationId,
                owned.PurchaseOrderId,
                TestContext.Current.CancellationToken
            )
        );
        Assert.IsType<GetPurchaseOrderResult.PermissionDenied>(
            await queries.GetLiveAsync(
                firstOrganization.UserId,
                firstOrganization.OrganizationId,
                owned.PurchaseOrderId,
                TestContext.Current.CancellationToken
            )
        );
        Assert.Equal(
            other.PurchaseOrderId,
            Assert
                .Single(
                    Assert
                        .IsType<ListPurchaseOrderSummariesResult.Listed>(
                            await queries.ListSummariesAsync(
                                secondOrganization.UserId,
                                secondOrganization.OrganizationId,
                                cancellationToken: TestContext.Current.CancellationToken
                            )
                        )
                        .Orders
                )
                .PurchaseOrderId
        );
        await using var denied = await ServicesAsync(
            postgres.GetConnectionString(),
            firstOrganization,
            migrate: false,
            allow: false
        );
        Assert.IsType<SetPurchaseOrderLineResult.PermissionDenied>(
            await SetLineAsync(denied, firstOrganization, owned.PurchaseOrderId, 1, 1m)
        );
        Assert.IsType<IssuePurchaseOrderResult.PermissionDenied>(
            await IssueAsync(denied, firstOrganization, owned.PurchaseOrderId, 1)
        );
        await using var deniedRead = denied.CreateAsyncScope();
        Assert.IsType<GetPurchaseOrderResult.PermissionDenied>(
            await deniedRead
                .ServiceProvider.GetRequiredService<IPurchaseOrderQueries>()
                .GetByCodeAsync(
                    firstOrganization.UserId,
                    firstOrganization.OrganizationId,
                    "PO-100",
                    TestContext.Current.CancellationToken
                )
        );
        await using var ownRead = first.CreateAsyncScope();
        var retained = Assert
            .IsType<GetPurchaseOrderResult.Found>(
                await ownRead
                    .ServiceProvider.GetRequiredService<IPurchaseOrderQueries>()
                    .GetLiveAsync(
                        firstOrganization.UserId,
                        firstOrganization.OrganizationId,
                        owned.PurchaseOrderId,
                        TestContext.Current.CancellationToken
                    )
            )
            .Order;
        Assert.Equal(1, retained.Version);
        Assert.Empty(retained.Lines);
    }

    [Fact]
    public async Task GetLive_RetainedLiteralV1Events_ReconstructsHistoricalMeaning()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:18.6").Build();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        var organization = Organization();
        await using var services = await ServicesAsync(
            postgres.GetConnectionString(),
            organization
        );
        Guid id = Guid.Parse("18ca41b8-a2b1-4ba5-912b-440b20485d58");
        await SetupAsync(
            postgres.GetConnectionString(),
            $$"""
            INSERT INTO purchasing.event_streams (id, organization_id, stream_type, version, created_at, updated_at)
            VALUES ('{{id}}', '{{organization.OrganizationId.Value}}', 'purchasing.purchase-order', 3, '2026-10-01T12:00:00Z', '2026-10-01T12:00:02Z');
            INSERT INTO purchasing.events (event_id, organization_id, stream_id, stream_version, event_name, schema_version, recorded_at, payload, metadata)
            VALUES
            ('18ca41b8-a2b1-4ba5-912b-440b20485d59', '{{organization.OrganizationId.Value}}', '{{id}}', 1, 'purchasing.purchase-order.drafted', 1, '2026-10-01T12:00:00Z', '{"code":"LEGACY-1","supplierReference":"SUP-OLD","currency":"EUR"}', '{}'),
            ('18ca41b8-a2b1-4ba5-912b-440b20485d60', '{{organization.OrganizationId.Value}}', '{{id}}', 2, 'purchasing.purchase-order.line-set', 1, '2026-10-01T12:00:01Z', '{"itemCode":"OLD-PART","quantity":12500,"unitPrice":12.50}', '{}'),
            ('18ca41b8-a2b1-4ba5-912b-440b20485d61', '{{organization.OrganizationId.Value}}', '{{id}}', 3, 'purchasing.purchase-order.issued', 1, '2026-10-01T12:00:02Z', '{}', '{}');
            """
        );
        await using var scope = services.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<IPurchaseOrderQueries>();
        var order = Assert
            .IsType<GetPurchaseOrderResult.Found>(
                await queries.GetLiveAsync(
                    organization.UserId,
                    organization.OrganizationId,
                    id,
                    TestContext.Current.CancellationToken
                )
            )
            .Order;
        Assert.Equal("LEGACY-1", order.Code);
        Assert.Equal("SUP-OLD", order.SupplierReference);
        Assert.Equal("issued", order.Status);
        Assert.Equal(3, order.Version);
        var line = Assert.Single(order.Lines);
        Assert.Equal("OLD-PART", line.ItemCode);
        // A retained fact exceeds today's command limit; replay must not revalidate it.
        Assert.Equal(12500m, line.Quantity);
        Assert.Equal(12.50m, line.UnitPrice);
        // Historic reads need no inline model. Writes fail closed instead of silently repairing it.
        await Assert.ThrowsAnyAsync<Exception>(() =>
            SetLineAsync(services, organization, id, 3, 1m)
        );
    }

    [Fact]
    public async Task SetLine_MultipleItems_ReplacesOnlyTargetAndMaintainsIndependentTotals()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:18.6").Build();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        var organization = Organization();
        await using var services = await ServicesAsync(
            postgres.GetConnectionString(),
            organization
        );
        var order = await CreateAsync(services, organization);
        Assert.IsType<SetPurchaseOrderLineResult.Changed>(
            await SetLineAsync(services, organization, order.PurchaseOrderId, 1, 2.5m)
        );
        Assert.IsType<SetPurchaseOrderLineResult.Changed>(
            await SetLineAsync(
                services,
                organization,
                order.PurchaseOrderId,
                2,
                3m,
                "PART-2",
                7.20m
            )
        );
        var revised = Assert
            .IsType<SetPurchaseOrderLineResult.Changed>(
                await SetLineAsync(services, organization, order.PurchaseOrderId, 3, 4m)
            )
            .Order;
        Assert.Equal(4, revised.Version);
        Assert.Equal(2, revised.Lines.Count);
        Assert.Equal(4m, Assert.Single(revised.Lines, x => x.ItemCode == "PART-1").Quantity);
        Assert.Equal(3m, Assert.Single(revised.Lines, x => x.ItemCode == "PART-2").Quantity);
        await using var scope = services.CreateAsyncScope();
        var summary = Assert.Single(
            Assert
                .IsType<ListPurchaseOrderSummariesResult.Listed>(
                    await scope
                        .ServiceProvider.GetRequiredService<IPurchaseOrderQueries>()
                        .ListSummariesAsync(
                            organization.UserId,
                            organization.OrganizationId,
                            cancellationToken: TestContext.Current.CancellationToken
                        )
                )
                .Orders
        );
        Assert.Equal(71.60m, summary.Total);
        Assert.Equal(2, summary.LineCount);
        Assert.Equal(4, summary.Version);
    }

    [Fact]
    public async Task SetLine_HistoryReadUnavailable_LoadsInlineWriteModel()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:18.6").Build();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        var organization = Organization();
        await using var admin = await ServicesAsync(postgres.GetConnectionString(), organization);
        var order = await CreateAsync(admin, organization);
        await SetupAsync(
            postgres.GetConnectionString(),
            """
            CREATE ROLE purchase_order_writer LOGIN PASSWORD 'test-only-writer';
            GRANT USAGE ON SCHEMA purchasing TO purchase_order_writer;
            GRANT SELECT, INSERT, UPDATE ON ALL TABLES IN SCHEMA purchasing TO purchase_order_writer;
            GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA purchasing TO purchase_order_writer;
            REVOKE SELECT ON purchasing.events FROM purchase_order_writer;
            GRANT SELECT (global_sequence) ON purchasing.events TO purchase_order_writer;
            """
        );
        var connection = new NpgsqlConnectionStringBuilder(postgres.GetConnectionString())
        {
            Username = "purchase_order_writer",
            Password = "test-only-writer",
        };
        await using var writer = await ServicesAsync(
            connection.ConnectionString,
            organization,
            migrate: false
        );
        var changed = Assert
            .IsType<SetPurchaseOrderLineResult.Changed>(
                await SetLineAsync(writer, organization, order.PurchaseOrderId, 1, 2.5m)
            )
            .Order;
        Assert.Equal(2, changed.Version);
        await AssertCurrentAsync(admin, organization, order.PurchaseOrderId, 2, 2.5m, 31.25m);
    }

    [Fact]
    public async Task SetLine_RecordedClockRegresses_FailsWithoutAdvancingHistoryOrViews()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:18.6").Build();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        var organization = Organization();
        var clock = new TestClock();
        await using var services = await ServicesAsync(
            postgres.GetConnectionString(),
            organization,
            clock
        );
        var order = await CreateAsync(services, organization);
        clock.Now = clock.Now.AddSeconds(-1);
        var exception = await Assert.ThrowsAnyAsync<Exception>(() =>
            SetLineAsync(services, organization, order.PurchaseOrderId, 1, 2.5m)
        );
        Assert.Equal("Purchase Order persistence integrity failure.", exception.Message);
        await using (var scope = services.CreateAsyncScope())
        {
            var queries = scope.ServiceProvider.GetRequiredService<IPurchaseOrderQueries>();
            var live = Assert
                .IsType<GetPurchaseOrderResult.Found>(
                    await queries.GetLiveAsync(
                        organization.UserId,
                        organization.OrganizationId,
                        order.PurchaseOrderId,
                        TestContext.Current.CancellationToken
                    )
                )
                .Order;
            Assert.Equal(1, live.Version);
            Assert.Empty(live.Lines);
            var summary = Assert.Single(
                Assert
                    .IsType<ListPurchaseOrderSummariesResult.Listed>(
                        await queries.ListSummariesAsync(
                            organization.UserId,
                            organization.OrganizationId,
                            cancellationToken: TestContext.Current.CancellationToken
                        )
                    )
                    .Orders
            );
            Assert.Equal(1, summary.Version);
            Assert.Equal(0m, summary.Total);
        }
        clock.Now = clock.Now.AddSeconds(2);
        Assert.IsType<SetPurchaseOrderLineResult.Changed>(
            await SetLineAsync(services, organization, order.PurchaseOrderId, 1, 2.5m)
        );
        await AssertCurrentAsync(services, organization, order.PurchaseOrderId, 2, 2.5m, 31.25m);
    }

    [Theory]
    [InlineData(99, "{\"code\":\"OLD-1\",\"supplierReference\":\"SUP-1\",\"currency\":\"EUR\"}")]
    [InlineData(1, "{\"code\":\"OLD-1\",\"currency\":\"EUR\"}")]
    public async Task GetLive_UnknownSchemaOrMissingRequiredPayload_FailsClosed(
        int schemaVersion,
        string payload
    )
    {
        await using var postgres = new PostgreSqlBuilder("postgres:18.6").Build();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        var organization = Organization();
        await using var services = await ServicesAsync(
            postgres.GetConnectionString(),
            organization
        );
        Guid id = Guid.CreateVersion7();
        await SetupAsync(
            postgres.GetConnectionString(),
            $$"""
            INSERT INTO purchasing.event_streams (id, organization_id, stream_type, version, created_at, updated_at)
            VALUES ('{{id}}', '{{organization.OrganizationId.Value}}', 'purchasing.purchase-order', 1, '2026-10-01T12:00:00Z', '2026-10-01T12:00:00Z');
            INSERT INTO purchasing.events (event_id, organization_id, stream_id, stream_version, event_name, schema_version, recorded_at, payload, metadata)
            VALUES ('{{Guid.CreateVersion7()}}', '{{organization.OrganizationId.Value}}', '{{id}}', 1, 'purchasing.purchase-order.drafted', {{schemaVersion}}, '2026-10-01T12:00:00Z', '{{payload}}', '{}');
            """
        );
        await using var scope = services.CreateAsyncScope();
        var exception = await Assert.ThrowsAnyAsync<Exception>(() =>
            scope
                .ServiceProvider.GetRequiredService<IPurchaseOrderQueries>()
                .GetLiveAsync(
                    organization.UserId,
                    organization.OrganizationId,
                    id,
                    TestContext.Current.CancellationToken
                )
        );
        Assert.Equal("Purchase Order persistence integrity failure.", exception.Message);
    }

    private static async Task AssertCurrentAsync(
        ServiceProvider services,
        OrganizationAccessContext organization,
        Guid id,
        long version,
        decimal quantity,
        decimal total
    )
    {
        await using var scope = services.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<IPurchaseOrderQueries>();
        var current = Assert
            .IsType<GetPurchaseOrderResult.Found>(
                await queries.GetByCodeAsync(
                    organization.UserId,
                    organization.OrganizationId,
                    "PO-100",
                    TestContext.Current.CancellationToken
                )
            )
            .Order;
        var live = Assert
            .IsType<GetPurchaseOrderResult.Found>(
                await queries.GetLiveAsync(
                    organization.UserId,
                    organization.OrganizationId,
                    id,
                    TestContext.Current.CancellationToken
                )
            )
            .Order;
        Assert.Equal(version, current.Version);
        Assert.Equal(version, live.Version);
        Assert.Equal(quantity, Assert.Single(current.Lines).Quantity);
        Assert.Equal(quantity, Assert.Single(live.Lines).Quantity);
        var summary = Assert.Single(
            Assert
                .IsType<ListPurchaseOrderSummariesResult.Listed>(
                    await queries.ListSummariesAsync(
                        organization.UserId,
                        organization.OrganizationId,
                        cancellationToken: TestContext.Current.CancellationToken
                    )
                )
                .Orders
        );
        Assert.Equal(version, summary.Version);
        Assert.Equal(1, summary.LineCount);
        Assert.Equal(total, summary.Total);
    }

    private static async Task SetupAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<ServiceProvider> ServicesAsync(
        string connection,
        OrganizationAccessContext organization,
        TimeProvider? clock = null,
        bool migrate = true,
        bool allow = true
    )
    {
        var services = new ServiceCollection();
        services.AddSingleton(NpgsqlDataSource.Create(connection));
        services.AddSingleton<IOrganizationContextAccessor>(new OrganizationAccessor(organization));
        services.AddSingleton<IOrganizationAuthorization>(new Authorization(allow));
        if (clock is not null)
            services.AddSingleton(clock);
        services.AddPurchasingModule();
        var provider = services.BuildServiceProvider(validateScopes: true);
        if (migrate)
            await provider.MigratePurchasingAsync(TestContext.Current.CancellationToken);
        return provider;
    }

    private sealed class OrganizationAccessor(OrganizationAccessContext organization)
        : IOrganizationContextAccessor
    {
        public OrganizationAccessContext? OrganizationContext => organization;
    }

    private sealed class Authorization(bool allow) : IOrganizationAuthorization
    {
        public Task<bool> HasPermissionAsync(
            UserId userId,
            OrganizationId organizationId,
            string permissionId,
            CancellationToken cancellationToken = default
        ) => Task.FromResult(allow && permissionId == PurchasingPermissionIds.PurchaseOrdersManage);
    }

    private sealed class TestClock : TimeProvider
    {
        internal DateTimeOffset Now { get; set; } = new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => Now;
    }
}
