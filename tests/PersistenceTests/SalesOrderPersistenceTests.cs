using Microsoft.Extensions.DependencyInjection;
using ModulithFoundry.Modules.Access.Contracts;
using ModulithFoundry.Modules.Inventory.Composition;
using ModulithFoundry.Modules.Inventory.Contracts;
using ModulithFoundry.Modules.Sales.Composition;
using ModulithFoundry.Modules.Sales.Contracts;
using Npgsql;
using Testcontainers.PostgreSql;

namespace ModulithFoundry.PersistenceTests;

public sealed class SalesOrderPersistenceTests
{
    [Fact]
    public async Task CreateDraft_ValidLines_PersistsImmutableSnapshotsAndRoundedAmounts()
    {
        await using PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:18.6").Build();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        OrganizationAccessContext organization = CreateOrganizationContext();
        await using ServiceProvider services = await CreateServicesAsync(
            postgres.GetConnectionString(),
            organization
        );
        StockItemView item = await CreateReferencesAsync(services, organization);
        SalesOrderView created = Assert
            .IsType<CreateDraftSalesOrderResult.Created>(
                await CreateDraftAsync(
                    services,
                    organization,
                    [new(item.StockItemId, 3.5m, 2.3456m), new(item.StockItemId, 1m, 0.005m)]
                )
            )
            .Order;
        Assert.Equal(1, created.OrderNumber);
        Assert.Equal("USD", created.Currency);
        Assert.Equal(8.22m, created.TotalAmount);
        Assert.Equal(2, created.Lines.Count);
        Assert.Equal(8.21m, created.Lines[0].LineAmount);
        Assert.Equal(0.01m, created.Lines[1].LineAmount);
        Assert.Equal(item.StockItemId, created.Lines[0].StockItemId);
        Assert.Equal("BOLT", created.Lines[0].Sku);
        Assert.Equal("Original bolt", created.Lines[0].Description);
        Assert.Equal("EA", created.Lines[0].BaseUnitCode);
        await using (AsyncServiceScope scope = services.CreateAsyncScope())
        {
            Assert.IsType<ChangeStockItemDescriptionResult.Changed>(
                await scope
                    .ServiceProvider.GetRequiredService<IStockItemAdministration>()
                    .ChangeDescriptionAsync(
                        new(
                            organization.UserId,
                            organization.OrganizationId,
                            "bolt",
                            "Changed bolt"
                        ),
                        TestContext.Current.CancellationToken
                    )
            );
        }
        SalesOrderView read = Assert
            .IsType<GetSalesOrderResult.Found>(
                await GetAsync(services, organization, created.OrderNumber)
            )
            .Order;
        Assert.Equal(created.SalesOrderId, read.SalesOrderId);
        Assert.Equal(created.Lines, read.Lines);
        Assert.Equal(8.22m, read.TotalAmount);
    }

    [Fact]
    public async Task CreateDraft_ConcurrentRequests_AssignsUniqueNumbersWithinEachOrganization()
    {
        await using PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:18.6").Build();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        OrganizationAccessContext organization = CreateOrganizationContext();
        await using ServiceProvider services = await CreateServicesAsync(
            postgres.GetConnectionString(),
            organization
        );
        StockItemView item = await CreateReferencesAsync(services, organization);
        CreateDraftSalesOrderResult[] results = await Task.WhenAll(
            Enumerable
                .Range(0, 8)
                .Select(_ =>
                    CreateDraftAsync(services, organization, [new(item.StockItemId, 1m, 1m)])
                )
        );
        long[] numbers =
        [
            .. results
                .Select(result =>
                    Assert.IsType<CreateDraftSalesOrderResult.Created>(result).Order.OrderNumber
                )
                .Order(),
        ];
        Assert.Equal(Enumerable.Range(1, 8).Select(number => (long)number), numbers);
        foreach (long number in numbers)
        {
            Assert.Equal(
                number,
                Assert
                    .IsType<GetSalesOrderResult.Found>(
                        await GetAsync(services, organization, number)
                    )
                    .Order.OrderNumber
            );
        }
        OrganizationAccessContext other = CreateOrganizationContext();
        services.GetRequiredService<TestOrganizationContextAccessor>().OrganizationContext = other;
        StockItemView otherItem = await CreateReferencesAsync(services, other);
        Assert.Equal(
            1,
            Assert
                .IsType<CreateDraftSalesOrderResult.Created>(
                    await CreateDraftAsync(services, other, [new(otherItem.StockItemId, 1m, 1m)])
                )
                .Order.OrderNumber
        );
    }

    [Fact]
    public async Task CreateDraft_AuditPersistenceFails_RollsBackOrderAndOwnedLinesButPermitsNumberGap()
    {
        await using PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:18.6").Build();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        OrganizationAccessContext organization = CreateOrganizationContext();
        await using ServiceProvider services = await CreateServicesAsync(
            postgres.GetConnectionString(),
            organization
        );
        StockItemView item = await CreateReferencesAsync(services, organization);
        await ExecuteFaultSetupAsync(
            postgres.GetConnectionString(),
            """
            CREATE FUNCTION sales.reject_order_audit() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                IF NEW.action = 'sales-order.created' THEN RAISE EXCEPTION 'injected order audit failure'; END IF;
                RETURN NEW;
            END; $$;
            CREATE TRIGGER reject_order_audit BEFORE INSERT ON sales.audit_entries
            FOR EACH ROW EXECUTE FUNCTION sales.reject_order_audit();
            """
        );
        await Assert.ThrowsAsync<Microsoft.EntityFrameworkCore.DbUpdateException>(() =>
            CreateDraftAsync(services, organization, [new(item.StockItemId, 1m, 1m)])
        );
        Assert.IsType<GetSalesOrderResult.NotFound>(await GetAsync(services, organization, 1));
        await ExecuteFaultSetupAsync(
            postgres.GetConnectionString(),
            "DROP TRIGGER reject_order_audit ON sales.audit_entries"
        );
        SalesOrderView retry = Assert
            .IsType<CreateDraftSalesOrderResult.Created>(
                await CreateDraftAsync(services, organization, [new(item.StockItemId, 1m, 1m)])
            )
            .Order;
        Assert.Equal(2, retry.OrderNumber);
        Assert.Single(retry.Lines);
    }

    [Fact]
    public async Task CreateDraft_InvalidQuantityPriceCurrencyOrLines_ReturnsSpecificFailures()
    {
        await using PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:18.6").Build();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        OrganizationAccessContext organization = CreateOrganizationContext();
        await using ServiceProvider services = await CreateServicesAsync(
            postgres.GetConnectionString(),
            organization
        );
        StockItemView item = await CreateReferencesAsync(services, organization);
        (decimal Quantity, decimal Price, string Currency, string Field)[] invalid =
        [
            (0m, 1m, "USD", "quantity"),
            (-1m, 1m, "USD", "quantity"),
            (0.0000001m, 1m, "USD", "quantity"),
            (10_000_000_000_000m, 1m, "USD", "quantity"),
            (1m, -1m, "USD", "unitPrice"),
            (1m, 0.00001m, "USD", "unitPrice"),
            (1m, 1_000_000_000_000_000m, "USD", "unitPrice"),
            (1m, 1m, "JPY", "currency"),
            (1m, 1m, "", "currency"),
            (1_000_000m, 1_000_000_000_000m, "USD", "lines"),
        ];
        foreach ((decimal quantity, decimal price, string currency, string field) in invalid)
        {
            Assert.Equal(
                field,
                Assert
                    .IsType<CreateDraftSalesOrderResult.Invalid>(
                        await CreateDraftAsync(
                            services,
                            organization,
                            [new(item.StockItemId, quantity, price)],
                            currency
                        )
                    )
                    .Field
            );
        }
        Assert.Equal(
            "lines",
            Assert
                .IsType<CreateDraftSalesOrderResult.Invalid>(
                    await CreateDraftAsync(services, organization, [])
                )
                .Field
        );
        Assert.Equal(
            "lines",
            Assert
                .IsType<CreateDraftSalesOrderResult.Invalid>(
                    await CreateDraftAsync(services, organization, null!)
                )
                .Field
        );
        Assert.Equal(
            "lines",
            Assert
                .IsType<CreateDraftSalesOrderResult.Invalid>(
                    await CreateDraftAsync(
                        services,
                        organization,
                        Enumerable
                            .Repeat(new DraftSalesOrderLine(item.StockItemId, 1m, 1m), 101)
                            .ToArray()
                    )
                )
                .Field
        );
        Assert.Equal(
            "lines",
            Assert
                .IsType<CreateDraftSalesOrderResult.Invalid>(
                    await CreateDraftAsync(services, organization, [null!])
                )
                .Field
        );
        Assert.Equal(
            "customerCode",
            Assert
                .IsType<CreateDraftSalesOrderResult.Invalid>(
                    await CreateDraftAsync(
                        services,
                        organization,
                        [new(item.StockItemId, 1m, 1m)],
                        customerCode: "wrong/code"
                    )
                )
                .Field
        );
        Assert.IsType<GetSalesOrderResult.NotFound>(await GetAsync(services, organization, 1));
        SalesOrderView free = Assert
            .IsType<CreateDraftSalesOrderResult.Created>(
                await CreateDraftAsync(
                    services,
                    organization,
                    [new(item.StockItemId, 0.000001m, 0m)],
                    " eur "
                )
            )
            .Order;
        Assert.Equal("EUR", free.Currency);
        Assert.Equal(0m, free.TotalAmount);
    }

    [Fact]
    public async Task CreateDraft_MissingInactiveOrForeignReferences_RejectsWithoutOpeningOrder()
    {
        await using PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:18.6").Build();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        OrganizationAccessContext organization = CreateOrganizationContext();
        await using ServiceProvider services = await CreateServicesAsync(
            postgres.GetConnectionString(),
            organization
        );
        StockItemView item = await CreateReferencesAsync(services, organization);
        Assert.IsType<CreateDraftSalesOrderResult.CustomerNotFound>(
            await CreateDraftAsync(
                services,
                organization,
                [new(item.StockItemId, 1m, 1m)],
                customerCode: "missing"
            )
        );
        var missingId = new StockItemId(Guid.CreateVersion7());
        Assert.Equal(
            [missingId],
            Assert
                .IsType<CreateDraftSalesOrderResult.ItemsUnavailable>(
                    await CreateDraftAsync(services, organization, [new(missingId, 1m, 1m)])
                )
                .MissingItemIds
        );
        await using (AsyncServiceScope scope = services.CreateAsyncScope())
        {
            Assert.IsType<SetStockItemActiveResult.Changed>(
                await scope
                    .ServiceProvider.GetRequiredService<IStockItemAdministration>()
                    .SetActiveAsync(
                        new(organization.UserId, organization.OrganizationId, item.Sku, false),
                        TestContext.Current.CancellationToken
                    )
            );
        }
        Assert.Equal(
            [item.StockItemId],
            Assert
                .IsType<CreateDraftSalesOrderResult.ItemsUnavailable>(
                    await CreateDraftAsync(services, organization, [new(item.StockItemId, 1m, 1m)])
                )
                .InactiveItemIds
        );
        Assert.IsType<GetSalesOrderResult.NotFound>(await GetAsync(services, organization, 1));
        OrganizationAccessContext other = CreateOrganizationContext();
        services.GetRequiredService<TestOrganizationContextAccessor>().OrganizationContext = other;
        await CreateReferencesAsync(services, other);
        CreateDraftSalesOrderResult.ItemsUnavailable foreign =
            Assert.IsType<CreateDraftSalesOrderResult.ItemsUnavailable>(
                await CreateDraftAsync(services, other, [new(item.StockItemId, 1m, 1m)])
            );
        Assert.Equal([item.StockItemId], foreign.MissingItemIds);
        Assert.Empty(foreign.InactiveItemIds);
    }

    [Fact]
    public async Task SalesOrders_ForgedContextOrRevokedPermissions_FailsClosed()
    {
        await using PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:18.6").Build();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        OrganizationAccessContext organization = CreateOrganizationContext();
        await using ServiceProvider services = await CreateServicesAsync(
            postgres.GetConnectionString(),
            organization
        );
        StockItemView item = await CreateReferencesAsync(services, organization);
        SalesOrderView order = Assert
            .IsType<CreateDraftSalesOrderResult.Created>(
                await CreateDraftAsync(services, organization, [new(item.StockItemId, 1m, 1m)])
            )
            .Order;
        OrganizationAccessContext other = CreateOrganizationContext();
        Assert.IsType<GetSalesOrderResult.PermissionDenied>(
            await GetAsync(services, other, order.OrderNumber)
        );
        Assert.IsType<CreateDraftSalesOrderResult.PermissionDenied>(
            await CreateDraftAsync(services, other, [new(item.StockItemId, 1m, 1m)])
        );
        services.GetRequiredService<TestOrganizationContextAccessor>().OrganizationContext = other;
        Assert.IsType<GetSalesOrderResult.NotFound>(
            await GetAsync(services, other, order.OrderNumber)
        );
        services.GetRequiredService<TestOrganizationContextAccessor>().OrganizationContext = null;
        Assert.IsType<GetSalesOrderResult.PermissionDenied>(
            await GetAsync(services, organization, order.OrderNumber)
        );
        services.GetRequiredService<TestOrganizationContextAccessor>().OrganizationContext =
            organization;
        Assert.IsType<GetSalesOrderResult.PermissionDenied>(
            await GetAsync(
                services,
                organization with
                {
                    UserId = new(Guid.CreateVersion7()),
                },
                order.OrderNumber
            )
        );
        services.GetRequiredService<TestOrganizationAuthorization>().Allow = false;
        Assert.IsType<CreateDraftSalesOrderResult.PermissionDenied>(
            await CreateDraftAsync(services, organization, [new(item.StockItemId, 1m, 1m)])
        );
        Assert.IsType<GetSalesOrderResult.PermissionDenied>(
            await GetAsync(services, organization, order.OrderNumber)
        );
        services.GetRequiredService<TestOrganizationAuthorization>().Allow = true;
        Assert.IsType<GetSalesOrderResult.NotFound>(await GetAsync(services, organization, 2));
    }

    private static async Task ExecuteFaultSetupAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task CreateDraft_MultipleAndRepeatedItems_UsesOneDistinctInventoryBatch()
    {
        await using PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:18.6").Build();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        OrganizationAccessContext organization = CreateOrganizationContext();
        await using ServiceProvider services = await CreateServicesAsync(
            postgres.GetConnectionString(),
            organization
        );
        StockItemView first = await CreateReferencesAsync(services, organization);
        StockItemView second;
        await using (AsyncServiceScope scope = services.CreateAsyncScope())
        {
            second = Assert
                .IsType<CreateStockItemResult.Created>(
                    await scope
                        .ServiceProvider.GetRequiredService<IStockItemAdministration>()
                        .CreateAsync(
                            new(
                                organization.UserId,
                                organization.OrganizationId,
                                "washer",
                                "Washer",
                                "ea"
                            ),
                            TestContext.Current.CancellationToken
                        )
                )
                .Item;
        }
        SalesOrderView order = Assert
            .IsType<CreateDraftSalesOrderResult.Created>(
                await CreateDraftAsync(
                    services,
                    organization,
                    [
                        new(first.StockItemId, 1m, 1m),
                        new(second.StockItemId, 2m, 2m),
                        new(first.StockItemId, 3m, 3m),
                    ]
                )
            )
            .Order;
        Assert.Equal(3, order.Lines.Count);
        TestReferenceCalls calls = services.GetRequiredService<TestReferenceCalls>();
        Assert.Equal(1, calls.Count);
        Assert.Equal([first.StockItemId, second.StockItemId], Assert.Single(calls.Requests));
    }

    private static OrganizationAccessContext CreateOrganizationContext() =>
        new(
            new UserId(Guid.CreateVersion7()),
            new OrganizationId(Guid.CreateVersion7()),
            new MembershipId(Guid.CreateVersion7()),
            "Sales Order Tests",
            "sales-order-tests",
            [SalesRoleIds.Clerk]
        );

    private static async Task<ServiceProvider> CreateServicesAsync(
        string connectionString,
        OrganizationAccessContext organization
    )
    {
        var services = new ServiceCollection();
        services.AddSingleton(NpgsqlDataSource.Create(connectionString));
        services.AddSingleton(new TestOrganizationContextAccessor(organization));
        services.AddSingleton<IOrganizationContextAccessor>(provider =>
            provider.GetRequiredService<TestOrganizationContextAccessor>()
        );
        services.AddSingleton<TestOrganizationAuthorization>();
        services.AddSingleton<IOrganizationAuthorization>(provider =>
            provider.GetRequiredService<TestOrganizationAuthorization>()
        );
        services.AddInventoryModule();
        ServiceDescriptor references = services.Single(descriptor =>
            descriptor.ServiceType == typeof(IStockItemReferences)
        );
        services.Remove(references);
        services.AddSingleton<TestReferenceCalls>();
        services.AddScoped<IStockItemReferences>(provider => new CountingStockItemReferences(
            (IStockItemReferences)
                ActivatorUtilities.CreateInstance(provider, references.ImplementationType!),
            provider.GetRequiredService<TestReferenceCalls>()
        ));
        services.AddSalesModule();
        ServiceProvider provider = services.BuildServiceProvider(validateScopes: true);
        await provider.MigrateInventoryAsync(TestContext.Current.CancellationToken);
        await provider.MigrateSalesAsync(TestContext.Current.CancellationToken);
        return provider;
    }

    private static async Task<StockItemView> CreateReferencesAsync(
        ServiceProvider services,
        OrganizationAccessContext organization
    )
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        Assert.IsType<CreateCustomerResult.Created>(
            await scope
                .ServiceProvider.GetRequiredService<ICustomerAdministration>()
                .CreateAsync(
                    new(organization.UserId, organization.OrganizationId, "buyer", "Buyer"),
                    TestContext.Current.CancellationToken
                )
        );
        return Assert
            .IsType<CreateStockItemResult.Created>(
                await scope
                    .ServiceProvider.GetRequiredService<IStockItemAdministration>()
                    .CreateAsync(
                        new(
                            organization.UserId,
                            organization.OrganizationId,
                            "bolt",
                            "Original bolt",
                            "ea"
                        ),
                        TestContext.Current.CancellationToken
                    )
            )
            .Item;
    }

    private static async Task<CreateDraftSalesOrderResult> CreateDraftAsync(
        ServiceProvider services,
        OrganizationAccessContext organization,
        IReadOnlyList<DraftSalesOrderLine> lines,
        string currency = "usd",
        string customerCode = "buyer"
    )
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        return await scope
            .ServiceProvider.GetRequiredService<ISalesOrderOperations>()
            .CreateDraftAsync(
                new(
                    organization.UserId,
                    organization.OrganizationId,
                    customerCode,
                    currency,
                    lines
                ),
                TestContext.Current.CancellationToken
            );
    }

    private static async Task<GetSalesOrderResult> GetAsync(
        ServiceProvider services,
        OrganizationAccessContext organization,
        long number
    )
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        return await scope
            .ServiceProvider.GetRequiredService<ISalesOrderOperations>()
            .GetByNumberAsync(
                organization.UserId,
                organization.OrganizationId,
                number,
                TestContext.Current.CancellationToken
            );
    }

    private sealed class TestOrganizationContextAccessor(OrganizationAccessContext organization)
        : IOrganizationContextAccessor
    {
        public OrganizationAccessContext? OrganizationContext { get; set; } = organization;
    }

    private sealed class TestReferenceCalls
    {
        internal int Count;
        internal System.Collections.Concurrent.ConcurrentQueue<StockItemId[]> Requests { get; } =
            new();
    }

    private sealed class CountingStockItemReferences(
        IStockItemReferences inner,
        TestReferenceCalls calls
    ) : IStockItemReferences
    {
        public Task<StockItemReferenceResolution> ResolveAsync(
            OrganizationId organizationId,
            IReadOnlyCollection<StockItemId> stockItemIds,
            CancellationToken cancellationToken = default
        )
        {
            Interlocked.Increment(ref calls.Count);
            calls.Requests.Enqueue([.. stockItemIds]);
            return inner.ResolveAsync(organizationId, stockItemIds, cancellationToken);
        }
    }

    private sealed class TestOrganizationAuthorization : IOrganizationAuthorization
    {
        public bool Allow { get; set; } = true;

        public Task<bool> HasPermissionAsync(
            UserId userId,
            OrganizationId organizationId,
            string permissionId,
            CancellationToken cancellationToken = default
        ) => Task.FromResult(Allow);
    }
}
