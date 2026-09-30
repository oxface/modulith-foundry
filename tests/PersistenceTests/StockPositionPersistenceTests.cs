using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using ModulithFoundry.Modules.Access.Contracts;
using ModulithFoundry.Modules.Inventory.Composition;
using ModulithFoundry.Modules.Inventory.Contracts;
using ModulithFoundry.Modules.Inventory.Persistence;
using ModulithFoundry.Modules.Inventory.StockPositions;
using ModulithFoundry.Modules.Inventory.StockPositions.Persistence;
using Npgsql;
using Testcontainers.PostgreSql;

namespace ModulithFoundry.PersistenceTests;

public sealed class StockPositionPersistenceTests
{
    [Fact]
    public async Task RecordReceipt_EventHistoryReadUnavailable_UsesInlineWriteState()
    {
        await using PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:18.6").Build();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        OrganizationAccessContext organization = CreateOrganizationContext();
        await using ServiceProvider services = await CreateServicesAsync(
            postgres.GetConnectionString(), organization);
        await CreateReferenceDataAsync(services, organization);
        Assert.IsType<RecordStockReceiptResult.Recorded>(await RecordReceiptAsync(
            services, organization, quantity: 10m, expectedVersion: 0));

        await using var connection = new NpgsqlConnection(postgres.GetConnectionString());
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand(
            """
            CREATE ROLE stock_position_writer LOGIN PASSWORD 'test-only-writer';
            GRANT USAGE ON SCHEMA inventory TO stock_position_writer;
            GRANT SELECT, INSERT, UPDATE ON ALL TABLES IN SCHEMA inventory TO stock_position_writer;
            GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA inventory TO stock_position_writer;
            REVOKE SELECT ON inventory.events FROM stock_position_writer;
            GRANT SELECT (global_sequence) ON inventory.events TO stock_position_writer;
            """, connection);
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        var writerConnection = new NpgsqlConnectionStringBuilder(postgres.GetConnectionString())
        {
            Username = "stock_position_writer",
            Password = "test-only-writer",
        };
        await using ServiceProvider writer = await CreateServicesAsync(
            writerConnection.ConnectionString, organization, migrate: false);

        StockPositionView recorded = Assert.IsType<RecordStockReceiptResult.Recorded>(
            await RecordReceiptAsync(writer, organization, quantity: 2m, expectedVersion: 2)).Position;

        Assert.Equal(12m, recorded.OnHandQuantity);
        Assert.Equal(3, recorded.Version);
        Assert.Equal(3, await CountStoredEventsAsync(postgres.GetConnectionString()));
    }

    [Fact]
    public async Task WriteModelLookup_MigrationRoundTrip_PreservesStreamIdentityAndState()
    {
        await using PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:18.6").Build();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        OrganizationAccessContext organization = CreateOrganizationContext();
        await using ServiceProvider services = await CreateServicesAsync(
            postgres.GetConnectionString(), organization);
        await CreateReferenceDataAsync(services, organization);
        StockPositionView original = Assert.IsType<RecordStockReceiptResult.Recorded>(await RecordReceiptAsync(
            services, organization, quantity: 10m, expectedVersion: 0)).Position;

        await using (AsyncServiceScope scope = services.CreateAsyncScope())
        {
            InventoryDbContext context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
            IMigrator migrator = context.GetService<IMigrator>();
            await migrator.MigrateAsync(
                "20260930131247_MoveStockPositionIdentityIntoStream", TestContext.Current.CancellationToken);
            await migrator.MigrateAsync(cancellationToken: TestContext.Current.CancellationToken);
        }

        Assert.IsType<RecordStockReceiptResult.VersionConflict>(await RecordReceiptAsync(
            services, organization, quantity: 1m, expectedVersion: 0));
        StockPositionView appended = Assert.IsType<RecordStockReceiptResult.Recorded>(await RecordReceiptAsync(
            services, organization, quantity: 1m, expectedVersion: 2)).Position;
        Assert.Equal(original.StockPositionId, appended.StockPositionId);
        Assert.Equal(11m, appended.OnHandQuantity);
        Assert.Equal(3, await CountStoredEventsAsync(postgres.GetConnectionString()));
    }

    [Fact]
    public async Task WriteModelLookup_MigrationWithMissingWriteModel_RequiresRepair()
    {
        await using PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:18.6").Build();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        OrganizationAccessContext organization = CreateOrganizationContext();
        await using ServiceProvider services = await CreateServicesAsync(
            postgres.GetConnectionString(), organization);
        await CreateReferenceDataAsync(services, organization);
        Assert.IsType<RecordStockReceiptResult.Recorded>(await RecordReceiptAsync(
            services, organization, quantity: 10m, expectedVersion: 0));

        await using AsyncServiceScope scope = services.CreateAsyncScope();
        InventoryDbContext context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        IMigrator migrator = context.GetService<IMigrator>();
        await migrator.MigrateAsync(
            "20260930131247_MoveStockPositionIdentityIntoStream", TestContext.Current.CancellationToken);
        await using var connection = new NpgsqlConnection(postgres.GetConnectionString());
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand("DELETE FROM inventory.stock_position_current", connection);
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);

        PostgresException failure = await Assert.ThrowsAsync<PostgresException>(() =>
            migrator.MigrateAsync(cancellationToken: TestContext.Current.CancellationToken));

        Assert.Contains("Repair Stock Position write models", failure.MessageText, StringComparison.Ordinal);
        Assert.Equal(2, await CountStoredEventsAsync(postgres.GetConnectionString()));
    }

    [Fact]
    public async Task RecordReceipt_LaggingProjection_RejectsAppendWithoutChangingHistory()
    {
        await using PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:18.6").Build();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        OrganizationAccessContext organization = CreateOrganizationContext();
        await using ServiceProvider services = await CreateServicesAsync(
            postgres.GetConnectionString(), organization);
        await CreateReferenceDataAsync(services, organization);
        Assert.IsType<RecordStockReceiptResult.Recorded>(await RecordReceiptAsync(
            services, organization, quantity: 10m, expectedVersion: 0));

        await using var connection = new NpgsqlConnection(postgres.GetConnectionString());
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand(
            "UPDATE inventory.stock_position_current SET version = 1", connection);
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<InvalidOperationException>(() => RecordReceiptAsync(
            services, organization, quantity: 1m, expectedVersion: 2));
        Assert.Equal(2, await CountStoredEventsAsync(postgres.GetConnectionString()));
    }

    [Fact]
    public async Task RecordReceipt_MissingWriteModelWithKnownVersion_RejectsAppend()
    {
        await using PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:18.6").Build();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        OrganizationAccessContext organization = CreateOrganizationContext();
        await using ServiceProvider services = await CreateServicesAsync(
            postgres.GetConnectionString(), organization);
        await CreateReferenceDataAsync(services, organization);
        StockPositionView recorded = Assert.IsType<RecordStockReceiptResult.Recorded>(await RecordReceiptAsync(
            services, organization, quantity: 10m, expectedVersion: 0)).Position;

        await using var connection = new NpgsqlConnection(postgres.GetConnectionString());
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand(
            "DELETE FROM inventory.stock_position_current", connection);
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);

        Assert.IsType<RecordStockReceiptResult.VersionConflict>(await RecordReceiptAsync(
            services, organization, quantity: 1m, expectedVersion: 2));
        Assert.Equal(2, await CountStoredEventsAsync(postgres.GetConnectionString()));

        await using AsyncServiceScope scope = services.CreateAsyncScope();
        StockPositionAggregate? live = await scope.ServiceProvider.GetRequiredService<StockPositionStore>()
            .LoadLiveAsync(recorded.StockPositionId.Value, TestContext.Current.CancellationToken);
        Assert.NotNull(live);
        Assert.Equal(10m, live.State!.OnHand.Value);
        Assert.Empty(live.UncommittedEvents);
    }

    [Fact]
    public async Task RecordReceipt_ConcurrentNewPosition_OpensOnlyOneStream()
    {
        await using PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:18.6").Build();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        OrganizationAccessContext organization = CreateOrganizationContext();
        await using ServiceProvider services = await CreateServicesAsync(
            postgres.GetConnectionString(), organization);
        await CreateReferenceDataAsync(services, organization);

        RecordStockReceiptResult[] results = await Task.WhenAll(
            RecordReceiptAsync(services, organization, quantity: 2m, expectedVersion: 0),
            RecordReceiptAsync(services, organization, quantity: 3m, expectedVersion: 0));

        StockPositionView winner = Assert.Single(
            results.OfType<RecordStockReceiptResult.Recorded>()).Position;
        Assert.True(winner.OnHandQuantity is 2m or 3m);
        Assert.Single(results, result => result is RecordStockReceiptResult.VersionConflict);
        Assert.Equal(2, await CountStoredEventsAsync(postgres.GetConnectionString()));
        Assert.Equal(1, await CountStockPositionProjectionsAsync(postgres.GetConnectionString()));
    }

    [Fact]
    public async Task RecordReceipt_NewPosition_AppendsOpeningAndReceiptAndProjectsCurrentState()
    {
        await using PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:18.6").Build();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        OrganizationAccessContext organization = CreateOrganizationContext();
        await using ServiceProvider services = await CreateServicesAsync(
            postgres.GetConnectionString(),
            organization);
        await CreateReferenceDataAsync(services, organization);

        RecordStockReceiptResult result;
        await using (AsyncServiceScope scope = services.CreateAsyncScope())
        {
            result = await scope.ServiceProvider.GetRequiredService<IStockPositions>()
                .RecordReceiptAsync(
                    new RecordStockReceiptCommand(
                        organization.UserId,
                        organization.OrganizationId,
                        "main",
                        "bolt-01",
                        12.5m,
                        ExpectedVersion: 0),
                    TestContext.Current.CancellationToken);
        }

        StockPositionView recorded = Assert.IsType<RecordStockReceiptResult.Recorded>(result).Position;
        Assert.Equal(2, recorded.Version);
        Assert.Equal(12.5m, recorded.OnHandQuantity);
        Assert.Equal(0m, recorded.ReservedQuantity);
        Assert.Equal(12.5m, recorded.AvailableQuantity);
        Assert.Equal("EA", recorded.BaseUnitCode);

        await using AsyncServiceScope readScope = services.CreateAsyncScope();
        GetStockPositionResult read = await readScope.ServiceProvider
            .GetRequiredService<IStockPositions>()
            .GetCurrentAsync(
                new GetStockPositionQuery(
                    organization.UserId,
                    organization.OrganizationId,
                    "MAIN",
                    "BOLT-01"),
                TestContext.Current.CancellationToken);
        Assert.Equal(recorded, Assert.IsType<GetStockPositionResult.Found>(read).Position);
    }

    [Fact]
    public async Task RecordReceipt_ConcurrentExpectedVersion_AllowsOneAppend()
    {
        await using PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:18.6").Build();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        OrganizationAccessContext organization = CreateOrganizationContext();
        await using ServiceProvider services = await CreateServicesAsync(
            postgres.GetConnectionString(),
            organization);
        await CreateReferenceDataAsync(services, organization);
        Assert.IsType<RecordStockReceiptResult.Recorded>(await RecordReceiptAsync(
            services,
            organization,
            quantity: 10m,
            expectedVersion: 0));

        RecordStockReceiptResult[] results = await Task.WhenAll(
            RecordReceiptAsync(services, organization, 2m, expectedVersion: 2),
            RecordReceiptAsync(services, organization, 3m, expectedVersion: 2));

        StockPositionView winner = Assert.Single(
            results.OfType<RecordStockReceiptResult.Recorded>()).Position;
        Assert.Equal(3, winner.Version);
        Assert.True(winner.OnHandQuantity is 12m or 13m);
        Assert.Single(results, result => result is RecordStockReceiptResult.VersionConflict);

        await using AsyncServiceScope scope = services.CreateAsyncScope();
        GetStockPositionResult current = await scope.ServiceProvider
            .GetRequiredService<IStockPositions>()
            .GetCurrentAsync(
                new GetStockPositionQuery(
                    organization.UserId,
                    organization.OrganizationId,
                    "main",
                    "bolt-01"),
                TestContext.Current.CancellationToken);
        Assert.Equal(winner, Assert.IsType<GetStockPositionResult.Found>(current).Position);
    }

    [Fact]
    public async Task RecordReceipt_PersistsStableEventEnvelopeAndSupportsExplicitLiveReplay()
    {
        await using PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:18.6").Build();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        OrganizationAccessContext organization = CreateOrganizationContext();
        await using ServiceProvider services = await CreateServicesAsync(
            postgres.GetConnectionString(),
            organization);
        await CreateReferenceDataAsync(services, organization);

        Assert.IsType<RecordStockReceiptResult.Recorded>(await RecordReceiptAsync(
            services,
            organization,
            quantity: 10.125m,
            expectedVersion: 0));
        StockPositionView rehydrated = Assert.IsType<RecordStockReceiptResult.Recorded>(
            await RecordReceiptAsync(
                services,
                organization,
                quantity: 1.875m,
                expectedVersion: 2)).Position;

        Assert.Equal(3, rehydrated.Version);
        Assert.Equal(12m, rehydrated.OnHandQuantity);

        await using (AsyncServiceScope scope = services.CreateAsyncScope())
        {
            StockPositionAggregate? live = await scope.ServiceProvider.GetRequiredService<StockPositionStore>()
                .LoadLiveAsync(rehydrated.StockPositionId.Value, TestContext.Current.CancellationToken);
            Assert.NotNull(live);
            Assert.Equal(12m, live.State!.OnHand.Value);
            Assert.Equal(3, live.ExpectedVersion);
            Assert.Empty(live.UncommittedEvents);
        }

        await using var connection = new NpgsqlConnection(postgres.GetConnectionString());
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand(
            """
            SELECT event_id, stream_version, event_name, schema_version,
                   payload::text, metadata::text, global_sequence
            FROM inventory.events
            WHERE organization_id = @organization_id
            ORDER BY stream_version
            """,
            connection);
        command.Parameters.AddWithValue("organization_id", organization.OrganizationId.Value);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(
            TestContext.Current.CancellationToken);
        var events = new List<StoredEventEnvelope>();
        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            events.Add(new StoredEventEnvelope(
                reader.GetGuid(0),
                reader.GetInt64(1),
                reader.GetString(2),
                reader.GetInt32(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetInt64(6)));
        }

        Assert.Equal([1L, 2L, 3L], events.Select(@event => @event.StreamVersion));
        Assert.Equal(
            [
                "inventory.stock-position.opened",
                "inventory.stock-position.received",
                "inventory.stock-position.received",
            ],
            events.Select(@event => @event.EventName));
        Assert.All(events, @event => Assert.Equal(1, @event.SchemaVersion));
        Assert.Equal(events.Count, events.Select(@event => @event.EventId).Distinct().Count());
        Assert.Equal(events.Count, events.Select(@event => @event.GlobalSequence).Distinct().Count());
        Assert.DoesNotContain(events, @event =>
            @event.EventName.Contains(',', StringComparison.Ordinal)
            || @event.Payload.Contains("ModulithFoundry", StringComparison.Ordinal)
            || @event.Metadata.Contains("ModulithFoundry", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RecordReceipt_InvalidQuantityDoesNotOpenStream()
    {
        await using PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:18.6").Build();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        OrganizationAccessContext organization = CreateOrganizationContext();
        await using ServiceProvider services = await CreateServicesAsync(
            postgres.GetConnectionString(),
            organization);
        await CreateReferenceDataAsync(services, organization);

        decimal[] invalidQuantities =
        [
            0m,
            -1m,
            0.0000001m,
            10_000_000_000_000m,
        ];
        foreach (decimal quantity in invalidQuantities)
        {
            RecordStockReceiptResult.Invalid invalid = Assert.IsType<RecordStockReceiptResult.Invalid>(
                await RecordReceiptAsync(services, organization, quantity, expectedVersion: 0));
            Assert.Equal("quantity", invalid.Field);
        }

        Assert.Equal(0, await CountStoredEventsAsync(postgres.GetConnectionString()));
        Assert.Equal(0, await CountStockPositionProjectionsAsync(postgres.GetConnectionString()));
    }

    [Fact]
    public async Task StockPositions_AreTenantScopedAndRequirePermission()
    {
        await using PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:18.6").Build();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        OrganizationAccessContext firstOrganization = CreateOrganizationContext();
        await using ServiceProvider services = await CreateServicesAsync(
            postgres.GetConnectionString(),
            firstOrganization);
        await CreateReferenceDataAsync(services, firstOrganization);
        StockPositionView firstPosition = Assert.IsType<RecordStockReceiptResult.Recorded>(await RecordReceiptAsync(
            services,
            firstOrganization,
            quantity: 5m,
            expectedVersion: 0)).Position;

        OrganizationAccessContext secondOrganization = CreateOrganizationContext();
        SetOrganizationContext(services, secondOrganization);
        await CreateReferenceDataAsync(services, secondOrganization);
        Assert.IsType<RecordStockReceiptResult.PermissionDenied>(await RecordReceiptAsync(
            services,
            firstOrganization,
            quantity: 1m,
            expectedVersion: 2));
        await using (AsyncServiceScope scope = services.CreateAsyncScope())
        {
            GetStockPositionResult result = await scope.ServiceProvider
                .GetRequiredService<IStockPositions>()
                .GetCurrentAsync(
                    new GetStockPositionQuery(
                        secondOrganization.UserId,
                        secondOrganization.OrganizationId,
                        "main",
                        "bolt-01"),
                    TestContext.Current.CancellationToken);
            Assert.IsType<GetStockPositionResult.NotFound>(result);
            Assert.Null(await scope.ServiceProvider.GetRequiredService<StockPositionStore>()
                .LoadLiveAsync(firstPosition.StockPositionId.Value, TestContext.Current.CancellationToken));
        }

        Assert.IsType<RecordStockReceiptResult.Recorded>(await RecordReceiptAsync(
            services,
            secondOrganization,
            quantity: 7m,
            expectedVersion: 0));

        services.GetRequiredService<TestOrganizationAuthorization>().Allow = false;
        Assert.IsType<RecordStockReceiptResult.PermissionDenied>(await RecordReceiptAsync(
            services,
            secondOrganization,
            quantity: 1m,
            expectedVersion: 2));
        Assert.Equal(4, await CountStoredEventsAsync(postgres.GetConnectionString()));
    }

    [Fact]
    public async Task RecordReceipt_ProjectionFailureRollsBackStreamAndEventAppend()
    {
        await using PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:18.6").Build();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        OrganizationAccessContext organization = CreateOrganizationContext();
        await using ServiceProvider services = await CreateServicesAsync(
            postgres.GetConnectionString(),
            organization);
        await CreateReferenceDataAsync(services, organization);
        Assert.IsType<RecordStockReceiptResult.Recorded>(await RecordReceiptAsync(
            services,
            organization,
            quantity: 8m,
            expectedVersion: 0));
        await CreateProjectionFailureTriggerAsync(postgres.GetConnectionString());

        DbUpdateException failure = await Assert.ThrowsAsync<DbUpdateException>(() => RecordReceiptAsync(
            services,
            organization,
            quantity: 2m,
            expectedVersion: 2));
        Assert.IsType<PostgresException>(failure.InnerException);

        await using var connection = new NpgsqlConnection(postgres.GetConnectionString());
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand(
            """
            SELECT s.version, p.version, p.on_hand_quantity,
                   (SELECT count(*) FROM inventory.events e WHERE e.stream_id = s.id)
            FROM inventory.event_streams s
            JOIN inventory.stock_position_current p ON p.stream_id = s.id
            WHERE s.organization_id = @organization_id
            """,
            connection);
        command.Parameters.AddWithValue("organization_id", organization.OrganizationId.Value);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(
            TestContext.Current.CancellationToken);
        Assert.True(await reader.ReadAsync(TestContext.Current.CancellationToken));
        Assert.Equal(2, reader.GetInt64(0));
        Assert.Equal(2, reader.GetInt64(1));
        Assert.Equal(8m, reader.GetDecimal(2));
        Assert.Equal(2, reader.GetInt64(3));
    }

    private static OrganizationAccessContext CreateOrganizationContext() =>
        new(
            new UserId(Guid.CreateVersion7()),
            new OrganizationId(Guid.CreateVersion7()),
            new MembershipId(Guid.CreateVersion7()),
            "Stock Position Test Organization",
            $"stock-position-{Guid.NewGuid():N}",
            [InventoryRoleIds.Manager]);

    private static async Task<ServiceProvider> CreateServicesAsync(
        string connectionString,
        OrganizationAccessContext organizationContext,
        bool migrate = true)
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
        if (migrate)
        {
            await provider.MigrateInventoryAsync(TestContext.Current.CancellationToken);
        }
        return provider;
    }

    private static async Task CreateReferenceDataAsync(
        ServiceProvider services,
        OrganizationAccessContext organization)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        CreateStockItemResult item = await scope.ServiceProvider
            .GetRequiredService<IStockItemAdministration>()
            .CreateAsync(
                new CreateStockItemCommand(
                    organization.UserId,
                    organization.OrganizationId,
                    "bolt-01",
                    "Zinc-plated bolt",
                    "ea"),
                TestContext.Current.CancellationToken);
        Assert.IsType<CreateStockItemResult.Created>(item);

        CreateStockingLocationResult location = await scope.ServiceProvider
            .GetRequiredService<IStockingLocationAdministration>()
            .CreateAsync(
                new CreateStockingLocationCommand(
                    organization.UserId,
                    organization.OrganizationId,
                    "main",
                    "Main warehouse"),
                TestContext.Current.CancellationToken);
        Assert.IsType<CreateStockingLocationResult.Created>(location);
    }

    private static async Task<RecordStockReceiptResult> RecordReceiptAsync(
        ServiceProvider services,
        OrganizationAccessContext organization,
        decimal quantity,
        long expectedVersion)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IStockPositions>()
            .RecordReceiptAsync(
                new RecordStockReceiptCommand(
                    organization.UserId,
                    organization.OrganizationId,
                    "main",
                    "bolt-01",
                    quantity,
                    expectedVersion),
                TestContext.Current.CancellationToken);
    }

    private static void SetOrganizationContext(
        ServiceProvider services,
        OrganizationAccessContext context) =>
        services.GetRequiredService<TestOrganizationContextAccessor>().OrganizationContext = context;

    private static Task<long> CountStoredEventsAsync(string connectionString) =>
        CountRowsAsync(connectionString, "SELECT count(*) FROM inventory.events");

    private static Task<long> CountStockPositionProjectionsAsync(string connectionString) =>
        CountRowsAsync(connectionString, "SELECT count(*) FROM inventory.stock_position_current");

    private static async Task<long> CountRowsAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        object? count = await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);
        return (long)Assert.IsType<long>(count);
    }

    private static async Task CreateProjectionFailureTriggerAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand(
            """
            CREATE FUNCTION inventory.reject_stock_position_update()
            RETURNS trigger
            LANGUAGE plpgsql
            AS $$
            BEGIN
                RAISE EXCEPTION 'injected projection failure';
            END;
            $$;

            CREATE TRIGGER reject_stock_position_update
            BEFORE UPDATE ON inventory.stock_position_current
            FOR EACH ROW
            EXECUTE FUNCTION inventory.reject_stock_position_update();
            """,
            connection);
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private sealed class TestOrganizationContextAccessor(OrganizationAccessContext context) :
        IOrganizationContextAccessor
    {
        public OrganizationAccessContext? OrganizationContext { get; set; } = context;
    }

    private sealed class TestOrganizationAuthorization : IOrganizationAuthorization
    {
        internal bool Allow { get; set; } = true;

        public Task<bool> HasPermissionAsync(
            UserId userId,
            OrganizationId organizationId,
            string permissionId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Allow);
    }

    private sealed record StoredEventEnvelope(
        Guid EventId,
        long StreamVersion,
        string EventName,
        int SchemaVersion,
        string Payload,
        string Metadata,
        long GlobalSequence);
}
