using Microsoft.Extensions.DependencyInjection;
using ModulithFoundry.Modules.Access.Contracts;
using ModulithFoundry.Modules.Inventory.Contracts;
using ModulithFoundry.Modules.Sales.Contracts;
using Testcontainers.PostgreSql;

namespace ModulithFoundry.PersistenceTests;

public sealed partial class SalesOrderPersistenceTests
{
    [Fact]
    public async Task Submit_Draft_PersistsSubmissionAndCuratedActivity()
    {
        await using PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:18.6").Build();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        OrganizationAccessContext organization = CreateOrganizationContext();
        await using ServiceProvider services = await CreateServicesAsync(
            postgres.GetConnectionString(),
            organization
        );
        StockItemView item = await CreateReferencesAsync(services, organization);
        SalesOrderView draft = Assert
            .IsType<CreateDraftSalesOrderResult.Created>(
                await CreateDraftAsync(services, organization, [new(item.StockItemId, 2m, 5m)])
            )
            .Order;
        Assert.Equal(SalesOrderStatus.Draft, draft.Status);
        Assert.Equal(1, draft.Version);
        Assert.Null(draft.SubmittedBy);
        Assert.Null(draft.SubmittedAt);

        SalesOrderView submitted = Assert
            .IsType<SubmitSalesOrderResult.Submitted>(
                await SubmitAsync(services, organization, draft.OrderNumber, draft.Version)
            )
            .Order;
        Assert.Equal(SalesOrderStatus.AwaitingApproval, submitted.Status);
        Assert.Equal(2, submitted.Version);
        Assert.Equal(organization.UserId, submitted.SubmittedBy);
        Assert.NotNull(submitted.SubmittedAt);
        SalesOrderView read = Assert
            .IsType<GetSalesOrderResult.Found>(
                await GetAsync(services, organization, draft.OrderNumber)
            )
            .Order;
        Assert.Equal(submitted.Status, read.Status);
        Assert.Equal(submitted.Version, read.Version);
        Assert.Equal(submitted.SubmittedAt, read.SubmittedAt);
        Assert.Equal(draft.Lines, read.Lines);

        IReadOnlyList<SalesOrderActivityEntry> activity = Assert
            .IsType<GetSalesOrderActivityResult.Found>(
                await GetActivityAsync(services, organization, draft.OrderNumber)
            )
            .Entries;
        Assert.Equal(
            [SalesOrderActivityKind.Created, SalesOrderActivityKind.Submitted],
            activity.Select(entry => entry.Kind)
        );
        Assert.All(activity, entry => Assert.Equal(organization.UserId, entry.ActorUserId));
        Assert.Equal([1L, 2L], activity.Select(entry => entry.OrderVersion));
        Assert.Equal(submitted.SubmittedAt, activity[1].OccurredAt);
    }

    [Fact]
    public async Task Submit_ConcurrentRequests_OnlyOneTransitionAndActivityCommit()
    {
        await using PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:18.6").Build();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        OrganizationAccessContext organization = CreateOrganizationContext();
        await using ServiceProvider services = await CreateServicesAsync(
            postgres.GetConnectionString(),
            organization
        );
        StockItemView item = await CreateReferencesAsync(services, organization);
        SalesOrderView draft = Assert
            .IsType<CreateDraftSalesOrderResult.Created>(
                await CreateDraftAsync(services, organization, [new(item.StockItemId, 2m, 5m)])
            )
            .Order;
        // Widen the database update race without substituting the module persistence path.
        await ExecuteFaultSetupAsync(
            postgres.GetConnectionString(),
            """
            CREATE FUNCTION sales.pause_order_update() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                PERFORM pg_sleep(0.25);
                RETURN NEW;
            END; $$;
            CREATE TRIGGER pause_order_update BEFORE UPDATE ON sales.orders
            FOR EACH ROW EXECUTE FUNCTION sales.pause_order_update();
            """
        );
        SubmitSalesOrderResult[] results = await Task.WhenAll(
            Enumerable
                .Range(0, 8)
                .Select(_ => SubmitAsync(services, organization, draft.OrderNumber, draft.Version))
        );
        Assert.Single(results.OfType<SubmitSalesOrderResult.Submitted>());
        Assert.Equal(7, results.OfType<SubmitSalesOrderResult.VersionConflict>().Count());
        SalesOrderView read = Assert
            .IsType<GetSalesOrderResult.Found>(
                await GetAsync(services, organization, draft.OrderNumber)
            )
            .Order;
        Assert.Equal(2, read.Version);
        Assert.Equal(SalesOrderStatus.AwaitingApproval, read.Status);
        IReadOnlyList<SalesOrderActivityEntry> activity = Assert
            .IsType<GetSalesOrderActivityResult.Found>(
                await GetActivityAsync(services, organization, draft.OrderNumber)
            )
            .Entries;
        Assert.Equal(
            [SalesOrderActivityKind.Created, SalesOrderActivityKind.Submitted],
            activity.Select(entry => entry.Kind)
        );
    }

    [Fact]
    public async Task Submit_StaleInvalidOrNonDraftCommand_DoesNotRepeatTransition()
    {
        await using PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:18.6").Build();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        OrganizationAccessContext organization = CreateOrganizationContext();
        await using ServiceProvider services = await CreateServicesAsync(
            postgres.GetConnectionString(),
            organization
        );
        StockItemView item = await CreateReferencesAsync(services, organization);
        SalesOrderView draft = Assert
            .IsType<CreateDraftSalesOrderResult.Created>(
                await CreateDraftAsync(services, organization, [new(item.StockItemId, 2m, 5m)])
            )
            .Order;
        Assert.IsType<SubmitSalesOrderResult.InvalidExpectedVersion>(
            await SubmitAsync(services, organization, draft.OrderNumber, 0)
        );
        Assert.IsType<SubmitSalesOrderResult.InvalidExpectedVersion>(
            await SubmitAsync(services, organization, draft.OrderNumber, -1)
        );
        Assert.IsType<SubmitSalesOrderResult.VersionConflict>(
            await SubmitAsync(services, organization, draft.OrderNumber, 100)
        );
        Assert.IsType<SubmitSalesOrderResult.NotFound>(
            await SubmitAsync(services, organization, 999, 1)
        );
        Assert.IsType<GetSalesOrderActivityResult.NotFound>(
            await GetActivityAsync(services, organization, 999)
        );
        Assert.IsType<SubmitSalesOrderResult.Submitted>(
            await SubmitAsync(services, organization, draft.OrderNumber, 1)
        );
        Assert.IsType<SubmitSalesOrderResult.VersionConflict>(
            await SubmitAsync(services, organization, draft.OrderNumber, 1)
        );
        Assert.IsType<SubmitSalesOrderResult.NotDraft>(
            await SubmitAsync(services, organization, draft.OrderNumber, 2)
        );
        IReadOnlyList<SalesOrderActivityEntry> activity = Assert
            .IsType<GetSalesOrderActivityResult.Found>(
                await GetActivityAsync(services, organization, draft.OrderNumber)
            )
            .Entries;
        Assert.Equal([1L, 2L], activity.Select(entry => entry.OrderVersion));
        Assert.Equal(
            2,
            Assert
                .IsType<GetSalesOrderResult.Found>(
                    await GetAsync(services, organization, draft.OrderNumber)
                )
                .Order.Version
        );
    }

    [Theory]
    [InlineData("audit_entries", "action = 'sales-order.submitted'")]
    [InlineData("order_activity", "kind = 'submitted'")]
    public async Task Submit_AuditOrActivityPersistenceFails_RollsBackTransitionAndAllowsRetry(
        string table,
        string condition
    )
    {
        await using PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:18.6").Build();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        OrganizationAccessContext organization = CreateOrganizationContext();
        await using ServiceProvider services = await CreateServicesAsync(
            postgres.GetConnectionString(),
            organization
        );
        StockItemView item = await CreateReferencesAsync(services, organization);
        SalesOrderView draft = Assert
            .IsType<CreateDraftSalesOrderResult.Created>(
                await CreateDraftAsync(services, organization, [new(item.StockItemId, 2m, 5m)])
            )
            .Order;
        // Identifiers/conditions are fixed test cases, never client-supplied SQL.
        await ExecuteFaultSetupAsync(
            postgres.GetConnectionString(),
            $"""
            CREATE FUNCTION sales.reject_submission() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                RAISE EXCEPTION 'injected submission persistence failure';
            END; $$;
            CREATE TRIGGER reject_submission BEFORE INSERT ON sales.{table}
            FOR EACH ROW WHEN (NEW.{condition}) EXECUTE FUNCTION sales.reject_submission();
            """
        );
        await Assert.ThrowsAsync<Microsoft.EntityFrameworkCore.DbUpdateException>(() =>
            SubmitAsync(services, organization, draft.OrderNumber, draft.Version)
        );
        SalesOrderView read = Assert
            .IsType<GetSalesOrderResult.Found>(
                await GetAsync(services, organization, draft.OrderNumber)
            )
            .Order;
        Assert.Equal(SalesOrderStatus.Draft, read.Status);
        Assert.Equal(1, read.Version);
        Assert.Null(read.SubmittedBy);
        Assert.Null(read.SubmittedAt);
        Assert.Equal(
            SalesOrderActivityKind.Created,
            Assert
                .Single(
                    Assert
                        .IsType<GetSalesOrderActivityResult.Found>(
                            await GetActivityAsync(services, organization, draft.OrderNumber)
                        )
                        .Entries
                )
                .Kind
        );
        await ExecuteFaultSetupAsync(
            postgres.GetConnectionString(),
            $"DROP TRIGGER reject_submission ON sales.{table};"
        );
        Assert.IsType<SubmitSalesOrderResult.Submitted>(
            await SubmitAsync(services, organization, draft.OrderNumber, draft.Version)
        );
        Assert.Equal(
            2,
            Assert
                .IsType<GetSalesOrderActivityResult.Found>(
                    await GetActivityAsync(services, organization, draft.OrderNumber)
                )
                .Entries.Count
        );
    }

    [Fact]
    public async Task SubmitAndActivity_MismatchedUnresolvedForeignOrDeniedContext_FailClosed()
    {
        await using PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:18.6").Build();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        OrganizationAccessContext organization = CreateOrganizationContext();
        await using ServiceProvider services = await CreateServicesAsync(
            postgres.GetConnectionString(),
            organization
        );
        StockItemView item = await CreateReferencesAsync(services, organization);
        SalesOrderView draft = Assert
            .IsType<CreateDraftSalesOrderResult.Created>(
                await CreateDraftAsync(services, organization, [new(item.StockItemId, 2m, 5m)])
            )
            .Order;
        OrganizationAccessContext forgedActor = organization with
        {
            UserId = new UserId(Guid.CreateVersion7()),
        };
        OrganizationAccessContext forgedOrganization = organization with
        {
            OrganizationId = new OrganizationId(Guid.CreateVersion7()),
        };
        foreach (OrganizationAccessContext forged in new[] { forgedActor, forgedOrganization })
        {
            Assert.IsType<SubmitSalesOrderResult.PermissionDenied>(
                await SubmitAsync(services, forged, draft.OrderNumber, draft.Version)
            );
            Assert.IsType<GetSalesOrderActivityResult.PermissionDenied>(
                await GetActivityAsync(services, forged, draft.OrderNumber)
            );
        }
        TestOrganizationContextAccessor accessor =
            services.GetRequiredService<TestOrganizationContextAccessor>();
        accessor.OrganizationContext = null;
        Assert.IsType<SubmitSalesOrderResult.PermissionDenied>(
            await SubmitAsync(services, organization, draft.OrderNumber, draft.Version)
        );
        Assert.IsType<GetSalesOrderActivityResult.PermissionDenied>(
            await GetActivityAsync(services, organization, draft.OrderNumber)
        );
        OrganizationAccessContext other = CreateOrganizationContext();
        accessor.OrganizationContext = other;
        Assert.IsType<SubmitSalesOrderResult.NotFound>(
            await SubmitAsync(services, other, draft.OrderNumber, draft.Version)
        );
        Assert.IsType<GetSalesOrderActivityResult.NotFound>(
            await GetActivityAsync(services, other, draft.OrderNumber)
        );
        accessor.OrganizationContext = organization;
        TestOrganizationAuthorization authorization =
            services.GetRequiredService<TestOrganizationAuthorization>();
        authorization.Allow = false;
        Assert.IsType<SubmitSalesOrderResult.PermissionDenied>(
            await SubmitAsync(services, organization, draft.OrderNumber, draft.Version)
        );
        Assert.IsType<GetSalesOrderActivityResult.PermissionDenied>(
            await GetActivityAsync(services, organization, draft.OrderNumber)
        );
        authorization.Allow = true;
        Assert.Equal(
            SalesOrderStatus.Draft,
            Assert
                .IsType<GetSalesOrderResult.Found>(
                    await GetAsync(services, organization, draft.OrderNumber)
                )
                .Order.Status
        );
        Assert.Equal(
            SalesOrderActivityKind.Created,
            Assert
                .Single(
                    Assert
                        .IsType<GetSalesOrderActivityResult.Found>(
                            await GetActivityAsync(services, organization, draft.OrderNumber)
                        )
                        .Entries
                )
                .Kind
        );
    }

    [Fact]
    public async Task Submit_PermissionDenied_RequiresDurableDenialAuditWithoutBusinessActivity()
    {
        await using PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:18.6").Build();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        OrganizationAccessContext organization = CreateOrganizationContext();
        await using ServiceProvider services = await CreateServicesAsync(
            postgres.GetConnectionString(),
            organization
        );
        StockItemView item = await CreateReferencesAsync(services, organization);
        SalesOrderView draft = Assert
            .IsType<CreateDraftSalesOrderResult.Created>(
                await CreateDraftAsync(services, organization, [new(item.StockItemId, 2m, 5m)])
            )
            .Order;
        await ExecuteFaultSetupAsync(
            postgres.GetConnectionString(),
            """
            CREATE FUNCTION sales.reject_denial_audit() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                RAISE EXCEPTION 'injected denial audit failure';
            END; $$;
            CREATE TRIGGER reject_denial_audit BEFORE INSERT ON sales.audit_entries
            FOR EACH ROW WHEN (NEW.action = 'sales-order.submit-denied') EXECUTE FUNCTION sales.reject_denial_audit();
            """
        );
        TestOrganizationAuthorization authorization =
            services.GetRequiredService<TestOrganizationAuthorization>();
        authorization.Allow = false;
        await Assert.ThrowsAsync<Microsoft.EntityFrameworkCore.DbUpdateException>(() =>
            SubmitAsync(services, organization, draft.OrderNumber, draft.Version)
        );
        await ExecuteFaultSetupAsync(
            postgres.GetConnectionString(),
            "DROP TRIGGER reject_denial_audit ON sales.audit_entries;"
        );
        Assert.IsType<SubmitSalesOrderResult.PermissionDenied>(
            await SubmitAsync(services, organization, draft.OrderNumber, draft.Version)
        );
        authorization.Allow = true;
        Assert.Equal(
            SalesOrderStatus.Draft,
            Assert
                .IsType<GetSalesOrderResult.Found>(
                    await GetAsync(services, organization, draft.OrderNumber)
                )
                .Order.Status
        );
        Assert.Equal(
            SalesOrderActivityKind.Created,
            Assert
                .Single(
                    Assert
                        .IsType<GetSalesOrderActivityResult.Found>(
                            await GetActivityAsync(services, organization, draft.OrderNumber)
                        )
                        .Entries
                )
                .Kind
        );
    }

    private static async Task<SubmitSalesOrderResult> SubmitAsync(
        ServiceProvider services,
        OrganizationAccessContext organization,
        long number,
        long expectedVersion
    )
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        return await scope
            .ServiceProvider.GetRequiredService<ISalesOrderOperations>()
            .SubmitAsync(
                new(organization.UserId, organization.OrganizationId, number, expectedVersion),
                TestContext.Current.CancellationToken
            );
    }

    private static async Task<GetSalesOrderActivityResult> GetActivityAsync(
        ServiceProvider services,
        OrganizationAccessContext organization,
        long number
    )
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        return await scope
            .ServiceProvider.GetRequiredService<ISalesOrderOperations>()
            .GetActivityAsync(
                organization.UserId,
                organization.OrganizationId,
                number,
                TestContext.Current.CancellationToken
            );
    }
}
