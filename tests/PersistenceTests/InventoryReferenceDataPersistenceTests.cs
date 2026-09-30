using Microsoft.Extensions.DependencyInjection;
using ModulithFoundry.Modules.Access.Contracts;
using ModulithFoundry.Modules.Inventory.Composition;
using ModulithFoundry.Modules.Inventory.Contracts;
using Npgsql;
using Testcontainers.PostgreSql;

namespace ModulithFoundry.PersistenceTests;

public sealed class InventoryReferenceDataPersistenceTests
{
    [Fact]
    public async Task StockItemAdministration_CreateAndResolveBatch_IsTenantScopedAndReportsInactiveItems()
    {
        await using PostgreSqlContainer postgres = CreatePostgresContainer();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        OrganizationAccessContext firstOrganization = CreateOrganizationContext();
        await using ServiceProvider services = await CreateServicesAsync(
            postgres.GetConnectionString(),
            firstOrganization);

        StockItemView first = Assert.IsType<CreateStockItemResult.Created>(
            await CreateStockItemAsync(
                services,
                firstOrganization,
                "  bolt-01 ",
                "  Zinc-plated bolt  ",
                " ea ")).Item;
        StockItemView second = Assert.IsType<CreateStockItemResult.Created>(
            await CreateStockItemAsync(
                services,
                firstOrganization,
                "washer-01",
                "Washer",
                "EA")).Item;
        Assert.Equal("BOLT-01", first.Sku);
        Assert.Equal("Zinc-plated bolt", first.Description);
        Assert.Equal("EA", first.BaseUnitCode);

        Assert.IsType<SetStockItemActiveResult.Changed>(await SetStockItemActiveAsync(
            services,
            firstOrganization,
            second.Sku,
            isActive: false));
        var missingId = new StockItemId(Guid.CreateVersion7());
        StockItemReferenceResolution resolved;
        await using (AsyncServiceScope scope = services.CreateAsyncScope())
        {
            resolved = await scope.ServiceProvider.GetRequiredService<IStockItemReferences>()
                .ResolveAsync(
                    firstOrganization.OrganizationId,
                    [first.StockItemId, second.StockItemId, missingId, first.StockItemId],
                    TestContext.Current.CancellationToken);
        }

        StockItemReference active = Assert.Single(resolved.Items);
        Assert.Equal(first.StockItemId, active.StockItemId);
        Assert.Equal([second.StockItemId], resolved.InactiveItemIds);
        Assert.Equal([missingId], resolved.MissingItemIds);

        OrganizationAccessContext secondOrganization = CreateOrganizationContext();
        SetOrganizationContext(services, secondOrganization);
        CreateStockItemResult sameSkuOtherTenant = await CreateStockItemAsync(
            services,
            secondOrganization,
            "bolt-01",
            "Different bolt",
            "EA");
        Assert.IsType<CreateStockItemResult.Created>(sameSkuOtherTenant);
        await using (AsyncServiceScope scope = services.CreateAsyncScope())
        {
            IStockItemReferences references = scope.ServiceProvider
                .GetRequiredService<IStockItemReferences>();
            await Assert.ThrowsAsync<InvalidOperationException>(() => references.ResolveAsync(
                firstOrganization.OrganizationId,
                [first.StockItemId],
                TestContext.Current.CancellationToken));

            StockItemReferenceResolution foreign = await references
                .ResolveAsync(
                    secondOrganization.OrganizationId,
                    [first.StockItemId],
                    TestContext.Current.CancellationToken);
            Assert.Empty(foreign.Items);
            Assert.Equal([first.StockItemId], foreign.MissingItemIds);
        }
    }

    [Fact]
    public async Task InventoryReferenceData_DuplicateCodesAndPermissionDenial_ReturnSpecificFailures()
    {
        await using PostgreSqlContainer postgres = CreatePostgresContainer();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        OrganizationAccessContext organization = CreateOrganizationContext();
        await using ServiceProvider services = await CreateServicesAsync(
            postgres.GetConnectionString(),
            organization);

        Assert.IsType<CreateStockItemResult.Created>(await CreateStockItemAsync(
            services,
            organization,
            "part-01",
            "Part",
            "EA"));
        Assert.IsType<CreateStockItemResult.SkuUnavailable>(await CreateStockItemAsync(
            services,
            organization,
            "PART-01",
            "Duplicate",
            "EA"));
        Assert.IsType<CreateStockingLocationResult.Created>(await CreateLocationAsync(
            services,
            organization,
            "main",
            "Main warehouse"));
        Assert.IsType<CreateStockingLocationResult.CodeUnavailable>(await CreateLocationAsync(
            services,
            organization,
            " MAIN ",
            "Duplicate warehouse"));

        OrganizationAccessContext foreignOrganization = CreateOrganizationContext();
        Assert.IsType<CreateStockItemResult.PermissionDenied>(await CreateStockItemAsync(
            services,
            foreignOrganization,
            "foreign-01",
            "Foreign item",
            "EA"));

        services.GetRequiredService<TestOrganizationAuthorization>().Allow = false;
        Assert.IsType<CreateStockItemResult.PermissionDenied>(await CreateStockItemAsync(
            services,
            organization,
            "part-02",
            "Denied part",
            "EA"));
        Assert.IsType<CreateStockingLocationResult.PermissionDenied>(await CreateLocationAsync(
            services,
            organization,
            "overflow",
            "Denied location"));

        await using var connection = new NpgsqlConnection(postgres.GetConnectionString());
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand(
            """
            SELECT action, outcome
            FROM inventory.audit_entries
            WHERE organization_id = @organization_id
            ORDER BY occurred_at, id
            """,
            connection);
        command.Parameters.AddWithValue("organization_id", organization.OrganizationId.Value);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(
            TestContext.Current.CancellationToken);
        var audits = new List<(string Action, string Outcome)>();
        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            audits.Add((reader.GetString(0), reader.GetString(1)));
        }

        Assert.Contains(("stock-item.created", "succeeded"), audits);
        Assert.Contains(("stocking-location.created", "succeeded"), audits);
        Assert.Contains(("stock-item.create-denied", "denied"), audits);
        Assert.Contains(("stocking-location.create-denied", "denied"), audits);
        Assert.Single(audits, audit => audit == ("stock-item.created", "succeeded"));
        Assert.Single(audits, audit => audit == ("stocking-location.created", "succeeded"));
    }

    [Fact]
    public async Task InventoryReferenceData_CommandLifecycle_ChangesMutableFieldsAndKeepsStableCodes()
    {
        await using PostgreSqlContainer postgres = CreatePostgresContainer();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        OrganizationAccessContext organization = CreateOrganizationContext();
        await using ServiceProvider services = await CreateServicesAsync(
            postgres.GetConnectionString(),
            organization);
        StockItemView item = Assert.IsType<CreateStockItemResult.Created>(
            await CreateStockItemAsync(services, organization, "gear-01", "Gear", "EA")).Item;
        StockingLocationView location = Assert.IsType<CreateStockingLocationResult.Created>(
            await CreateLocationAsync(services, organization, "north", "North store")).Location;

        await using (AsyncServiceScope scope = services.CreateAsyncScope())
        {
            IStockItemAdministration items = scope.ServiceProvider
                .GetRequiredService<IStockItemAdministration>();
            Assert.IsType<ChangeStockItemDescriptionResult.Changed>(await items.ChangeDescriptionAsync(
                new ChangeStockItemDescriptionCommand(
                    organization.UserId,
                    organization.OrganizationId,
                    item.Sku,
                    "Precision gear"),
                TestContext.Current.CancellationToken));
            Assert.IsType<SetStockItemActiveResult.Changed>(await items.SetActiveAsync(
                new SetStockItemActiveCommand(
                    organization.UserId,
                    organization.OrganizationId,
                    item.Sku,
                    false),
                TestContext.Current.CancellationToken));
        }

        await using (AsyncServiceScope scope = services.CreateAsyncScope())
        {
            IStockingLocationAdministration locations = scope.ServiceProvider
                .GetRequiredService<IStockingLocationAdministration>();
            Assert.IsType<RenameStockingLocationResult.Renamed>(await locations.RenameAsync(
                new RenameStockingLocationCommand(
                    organization.UserId,
                    organization.OrganizationId,
                    location.Code,
                    "North warehouse"),
                TestContext.Current.CancellationToken));
            Assert.IsType<SetStockingLocationActiveResult.Changed>(await locations.SetActiveAsync(
                new SetStockingLocationActiveCommand(
                    organization.UserId,
                    organization.OrganizationId,
                    location.Code,
                    false),
                TestContext.Current.CancellationToken));
        }

        await using (AsyncServiceScope scope = services.CreateAsyncScope())
        {
            ListStockItemsResult.Listed items = Assert.IsType<ListStockItemsResult.Listed>(
                await scope.ServiceProvider.GetRequiredService<IStockItemAdministration>().ListAsync(
                    organization.UserId,
                    organization.OrganizationId,
                    TestContext.Current.CancellationToken));
            StockItemView changedItem = Assert.Single(items.Items);
            Assert.Equal(item.StockItemId, changedItem.StockItemId);
            Assert.Equal("GEAR-01", changedItem.Sku);
            Assert.Equal("Precision gear", changedItem.Description);
            Assert.Equal("EA", changedItem.BaseUnitCode);
            Assert.False(changedItem.IsActive);

            ListStockingLocationsResult.Listed locations =
                Assert.IsType<ListStockingLocationsResult.Listed>(
                    await scope.ServiceProvider.GetRequiredService<IStockingLocationAdministration>()
                        .ListAsync(
                            organization.UserId,
                            organization.OrganizationId,
                            TestContext.Current.CancellationToken));
            StockingLocationView changedLocation = Assert.Single(locations.Locations);
            Assert.Equal(location.StockingLocationId, changedLocation.StockingLocationId);
            Assert.Equal("NORTH", changedLocation.Code);
            Assert.Equal("North warehouse", changedLocation.Name);
            Assert.False(changedLocation.IsActive);
        }
    }

    [Fact]
    public async Task CreateStockItem_ConcurrentCanonicalSku_AllowsOneWinner()
    {
        await using PostgreSqlContainer postgres = CreatePostgresContainer();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        OrganizationAccessContext organization = CreateOrganizationContext();
        await using ServiceProvider services = await CreateServicesAsync(
            postgres.GetConnectionString(),
            organization);

        CreateStockItemResult[] results = await Task.WhenAll(
            CreateStockItemAsync(services, organization, "race-01", "First", "EA"),
            CreateStockItemAsync(services, organization, " RACE-01 ", "Second", "EA"));

        Assert.Single(results, result => result is CreateStockItemResult.Created);
        Assert.Single(results, result => result is CreateStockItemResult.SkuUnavailable);
    }

    private static PostgreSqlContainer CreatePostgresContainer() =>
        new PostgreSqlBuilder("postgres:18.6").Build();

    private static OrganizationAccessContext CreateOrganizationContext() =>
        new(
            new UserId(Guid.CreateVersion7()),
            new OrganizationId(Guid.CreateVersion7()),
            new MembershipId(Guid.CreateVersion7()),
            "Inventory Test Organization",
            $"inventory-{Guid.NewGuid():N}",
            [InventoryRoleIds.Manager]);

    private static async Task<ServiceProvider> CreateServicesAsync(
        string connectionString,
        OrganizationAccessContext organizationContext)
    {
        var services = new ServiceCollection();
        services.AddSingleton(NpgsqlDataSource.Create(connectionString));
        services.AddSingleton(new TestOrganizationContextAccessor(organizationContext));
        services.AddSingleton<IOrganizationContextAccessor>(provider =>
            provider.GetRequiredService<TestOrganizationContextAccessor>());
        services.AddSingleton<TestOrganizationAuthorization>();
        services.AddSingleton<IOrganizationAuthorization>(provider =>
            provider.GetRequiredService<TestOrganizationAuthorization>());
        services.AddSingleton(TimeProvider.System);
        services.AddInventoryModule();
        ServiceProvider provider = services.BuildServiceProvider(validateScopes: true);
        await provider.MigrateInventoryAsync(TestContext.Current.CancellationToken);
        return provider;
    }

    private static void SetOrganizationContext(
        ServiceProvider services,
        OrganizationAccessContext context) =>
        services.GetRequiredService<TestOrganizationContextAccessor>().OrganizationContext = context;

    private static async Task<CreateStockItemResult> CreateStockItemAsync(
        ServiceProvider services,
        OrganizationAccessContext context,
        string sku,
        string description,
        string baseUnitCode)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IStockItemAdministration>().CreateAsync(
            new CreateStockItemCommand(
                context.UserId,
                context.OrganizationId,
                sku,
                description,
                baseUnitCode),
            TestContext.Current.CancellationToken);
    }

    private static async Task<SetStockItemActiveResult> SetStockItemActiveAsync(
        ServiceProvider services,
        OrganizationAccessContext context,
        string sku,
        bool isActive)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IStockItemAdministration>().SetActiveAsync(
            new SetStockItemActiveCommand(
                context.UserId,
                context.OrganizationId,
                sku,
                isActive),
            TestContext.Current.CancellationToken);
    }

    private static async Task<CreateStockingLocationResult> CreateLocationAsync(
        ServiceProvider services,
        OrganizationAccessContext context,
        string code,
        string name)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IStockingLocationAdministration>()
            .CreateAsync(
            new CreateStockingLocationCommand(
                context.UserId,
                context.OrganizationId,
                code,
                name),
            TestContext.Current.CancellationToken);
    }

    private sealed class TestOrganizationContextAccessor(OrganizationAccessContext context) :
        IOrganizationContextAccessor
    {
        public OrganizationAccessContext? OrganizationContext { get; set; } = context;
    }

    private sealed class TestOrganizationAuthorization : IOrganizationAuthorization
    {
        public bool Allow { get; set; } = true;

        public Task<bool> HasPermissionAsync(
            UserId userId,
            OrganizationId organizationId,
            string permissionId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Allow);
    }
}
