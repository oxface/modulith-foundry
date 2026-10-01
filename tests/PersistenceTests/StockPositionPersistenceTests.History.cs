using Microsoft.Extensions.DependencyInjection;
using ModulithFoundry.Modules.Access.Contracts;
using ModulithFoundry.Modules.Inventory.Contracts;
using ModulithFoundry.Modules.Inventory.StockPositions.Persistence;
using Npgsql;
using Testcontainers.PostgreSql;

namespace ModulithFoundry.PersistenceTests;

public sealed partial class StockPositionPersistenceTests
{
    [Fact]
    public async Task GetHistoricalState_VersionAndInclusiveRecordedTime_ReconstructsRecordedFacts()
    {
        await using PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:18.6").Build();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        OrganizationAccessContext organization = CreateOrganizationContext();
        DateTimeOffset openedAt = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);
        var clock = new HistoryTimeProvider(openedAt);
        await using ServiceProvider services = await CreateServicesAsync(
            postgres.GetConnectionString(),
            organization,
            timeProvider: clock
        );
        await CreateReferenceDataAsync(services, organization);
        Assert.IsType<RecordStockReceiptResult.Recorded>(
            await RecordReceiptAsync(services, organization, quantity: 10m, expectedVersion: 0)
        );
        clock.UtcNow = openedAt.AddMinutes(1);
        Assert.IsType<RecordStockReceiptResult.Recorded>(
            await RecordReceiptAsync(services, organization, quantity: 2m, expectedVersion: 2)
        );
        Assert.IsType<RecordStockReceiptResult.Recorded>(
            await RecordReceiptAsync(services, organization, quantity: 3m, expectedVersion: 3)
        );

        // Sequence allocation within an EF batch is not the stream's logical event order.
        await ExecuteHistorySqlAsync(
            postgres.GetConnectionString(),
            """
            UPDATE inventory.events SET global_sequence = global_sequence + 1000;
            UPDATE inventory.events SET global_sequence = 100 - stream_version;
            """
        );

        await using AsyncServiceScope scope = services.CreateAsyncScope();
        IStockPositionOperations positions =
            scope.ServiceProvider.GetRequiredService<IStockPositionOperations>();
        StockPositionView opening = Assert
            .IsType<GetStockPositionResult.Found>(
                await positions.GetAtVersionAsync(
                    new(
                        organization.UserId,
                        organization.OrganizationId,
                        "main",
                        "bolt-01",
                        Version: 1
                    ),
                    TestContext.Current.CancellationToken
                )
            )
            .Position;
        Assert.Equal(0m, opening.OnHandQuantity);
        Assert.Equal(1, opening.Version);

        StockPositionView intermediate = Assert
            .IsType<GetStockPositionResult.Found>(
                await positions.GetAtVersionAsync(
                    new(
                        organization.UserId,
                        organization.OrganizationId,
                        "main",
                        "bolt-01",
                        Version: 3
                    ),
                    TestContext.Current.CancellationToken
                )
            )
            .Position;
        Assert.Equal(12m, intermediate.OnHandQuantity);

        StockPositionView initial = Assert
            .IsType<GetStockPositionResult.Found>(
                await positions.GetAsOfAsync(
                    new(
                        organization.UserId,
                        organization.OrganizationId,
                        "main",
                        "bolt-01",
                        openedAt
                    ),
                    TestContext.Current.CancellationToken
                )
            )
            .Position;
        Assert.Equal(10m, initial.OnHandQuantity);
        Assert.Equal(2, initial.Version);

        StockPositionView tied = Assert
            .IsType<GetStockPositionResult.Found>(
                await positions.GetAsOfAsync(
                    new(
                        organization.UserId,
                        organization.OrganizationId,
                        "main",
                        "bolt-01",
                        clock.UtcNow.ToOffset(TimeSpan.FromHours(3))
                    ),
                    TestContext.Current.CancellationToken
                )
            )
            .Position;
        Assert.Equal(15m, tied.OnHandQuantity);
        Assert.Equal(4, tied.Version);

        Assert.IsType<GetStockPositionResult.NotFound>(
            await positions.GetAsOfAsync(
                new(
                    organization.UserId,
                    organization.OrganizationId,
                    "main",
                    "bolt-01",
                    openedAt.AddTicks(-10)
                ),
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task HistoricalQueries_InvalidSelectors_ReturnExpectedFailures()
    {
        await using PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:18.6").Build();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        OrganizationAccessContext organization = CreateOrganizationContext();
        await using ServiceProvider services = await CreateServicesAsync(
            postgres.GetConnectionString(),
            organization
        );
        await CreateReferenceDataAsync(services, organization);
        Assert.IsType<RecordStockReceiptResult.Recorded>(
            await RecordReceiptAsync(services, organization, quantity: 10m, expectedVersion: 0)
        );
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        IStockPositionOperations positions =
            scope.ServiceProvider.GetRequiredService<IStockPositionOperations>();

        Assert.IsType<GetStockPositionResult.Invalid>(
            await positions.GetAtVersionAsync(
                new(organization.UserId, organization.OrganizationId, "main", "bolt-01", 0),
                TestContext.Current.CancellationToken
            )
        );
        Assert.IsType<GetStockPositionResult.NotFound>(
            await positions.GetAtVersionAsync(
                new(organization.UserId, organization.OrganizationId, "main", "bolt-01", 3),
                TestContext.Current.CancellationToken
            )
        );
        var history = new GetStockPositionHistoryQuery(
            organization.UserId,
            organization.OrganizationId,
            "main",
            "bolt-01"
        );
        Assert.IsType<GetStockPositionHistoryResult.Invalid>(
            await positions.GetHistoryAsync(
                history with
                {
                    AfterVersion = -1,
                },
                TestContext.Current.CancellationToken
            )
        );
        Assert.IsType<GetStockPositionHistoryResult.Invalid>(
            await positions.GetHistoryAsync(
                history with
                {
                    Limit = 0,
                },
                TestContext.Current.CancellationToken
            )
        );
        Assert.IsType<GetStockPositionHistoryResult.Invalid>(
            await positions.GetHistoryAsync(
                history with
                {
                    Limit = GetStockPositionHistoryQuery.MaximumPageSize + 1,
                },
                TestContext.Current.CancellationToken
            )
        );
        Assert.Empty(
            Assert
                .IsType<GetStockPositionHistoryResult.Found>(
                    await positions.GetHistoryAsync(
                        history with
                        {
                            AfterVersion = 2,
                        },
                        TestContext.Current.CancellationToken
                    )
                )
                .History.Entries
        );
    }

    [Fact]
    public async Task HistoricalQueries_TenantMismatchAndDeniedPermission_DoNotExposeHistory()
    {
        await using PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:18.6").Build();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        OrganizationAccessContext first = CreateOrganizationContext();
        await using ServiceProvider services = await CreateServicesAsync(
            postgres.GetConnectionString(),
            first
        );
        await CreateReferenceDataAsync(services, first);
        Assert.IsType<RecordStockReceiptResult.Recorded>(
            await RecordReceiptAsync(services, first, quantity: 10m, expectedVersion: 0)
        );
        OrganizationAccessContext second = CreateOrganizationContext();
        SetOrganizationContext(services, second);
        await CreateReferenceDataAsync(services, second);
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        IStockPositionOperations positions =
            scope.ServiceProvider.GetRequiredService<IStockPositionOperations>();

        Assert.IsType<GetStockPositionResult.PermissionDenied>(
            await positions.GetAtVersionAsync(
                new(first.UserId, first.OrganizationId, "main", "bolt-01", 2),
                TestContext.Current.CancellationToken
            )
        );
        Assert.IsType<GetStockPositionResult.NotFound>(
            await positions.GetAtVersionAsync(
                new(second.UserId, second.OrganizationId, "main", "bolt-01", 2),
                TestContext.Current.CancellationToken
            )
        );
        Assert.IsType<GetStockPositionHistoryResult.NotFound>(
            await positions.GetHistoryAsync(
                new(second.UserId, second.OrganizationId, "main", "bolt-01"),
                TestContext.Current.CancellationToken
            )
        );
        services.GetRequiredService<TestOrganizationAuthorization>().Allow = false;
        Assert.IsType<GetStockPositionHistoryResult.PermissionDenied>(
            await positions.GetHistoryAsync(
                new(second.UserId, second.OrganizationId, "main", "bolt-01"),
                TestContext.Current.CancellationToken
            )
        );
        Assert.IsType<GetStockPositionResult.PermissionDenied>(
            await positions.GetAsOfAsync(
                new(
                    second.UserId,
                    second.OrganizationId,
                    "main",
                    "bolt-01",
                    DateTimeOffset.MaxValue
                ),
                TestContext.Current.CancellationToken
            )
        );
    }

    [Theory]
    [InlineData(
        "DELETE FROM inventory.events WHERE stream_version = 2",
        (int)StockPositionIntegrityFailure.HistoryGap
    )]
    [InlineData(
        "UPDATE inventory.events SET schema_version = 99 WHERE stream_version = 2",
        (int)StockPositionIntegrityFailure.UnknownEvent
    )]
    [InlineData(
        "UPDATE inventory.events SET payload = '{\"quantity\": \"unreadable\"}' WHERE stream_version = 2",
        (int)StockPositionIntegrityFailure.InvalidEventPayload
    )]
    public async Task GetHistoricalState_CorruptHistory_RaisesStructuredIntegrityFault(
        string damageSql,
        int expectedFailure
    )
    {
        await using PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:18.6").Build();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        OrganizationAccessContext organization = CreateOrganizationContext();
        await using ServiceProvider services = await CreateServicesAsync(
            postgres.GetConnectionString(),
            organization
        );
        await CreateReferenceDataAsync(services, organization);
        StockPositionView first = Assert
            .IsType<RecordStockReceiptResult.Recorded>(
                await RecordReceiptAsync(services, organization, quantity: 10m, expectedVersion: 0)
            )
            .Position;
        Assert.IsType<RecordStockReceiptResult.Recorded>(
            await RecordReceiptAsync(services, organization, quantity: 2m, expectedVersion: 2)
        );
        await ExecuteHistorySqlAsync(postgres.GetConnectionString(), damageSql);
        await using AsyncServiceScope scope = services.CreateAsyncScope();

        StockPositionIntegrityException failure =
            await Assert.ThrowsAsync<StockPositionIntegrityException>(() =>
                scope
                    .ServiceProvider.GetRequiredService<IStockPositionOperations>()
                    .GetAtVersionAsync(
                        new(organization.UserId, organization.OrganizationId, "main", "bolt-01", 3),
                        TestContext.Current.CancellationToken
                    )
            );

        Assert.Equal(first.StockPositionId.Value, failure.StreamId);
        Assert.Equal((StockPositionIntegrityFailure)expectedFailure, failure.Failure);
    }

    [Fact]
    public async Task HistoricalReads_ReadOnlyDatabaseRole_DoesNotAppendEventsOrAudit()
    {
        await using PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:18.6").Build();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        OrganizationAccessContext organization = CreateOrganizationContext();
        DateTimeOffset recordedAt = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);
        await using ServiceProvider services = await CreateServicesAsync(
            postgres.GetConnectionString(),
            organization,
            timeProvider: new HistoryTimeProvider(recordedAt)
        );
        await CreateReferenceDataAsync(services, organization);
        Assert.IsType<RecordStockReceiptResult.Recorded>(
            await RecordReceiptAsync(services, organization, quantity: 10m, expectedVersion: 0)
        );
        await ExecuteHistorySqlAsync(
            postgres.GetConnectionString(),
            """
            CREATE ROLE stock_history_reader LOGIN PASSWORD 'test-only-reader';
            GRANT USAGE ON SCHEMA inventory TO stock_history_reader;
            GRANT SELECT ON ALL TABLES IN SCHEMA inventory TO stock_history_reader;
            """
        );
        var readerConnection = new NpgsqlConnectionStringBuilder(postgres.GetConnectionString())
        {
            Username = "stock_history_reader",
            Password = "test-only-reader",
        };
        await using ServiceProvider reader = await CreateServicesAsync(
            readerConnection.ConnectionString,
            organization,
            migrate: false
        );
        await using AsyncServiceScope scope = reader.CreateAsyncScope();
        IStockPositionOperations positions =
            scope.ServiceProvider.GetRequiredService<IStockPositionOperations>();

        Assert.IsType<GetStockPositionResult.Found>(
            await positions.GetAtVersionAsync(
                new(organization.UserId, organization.OrganizationId, "main", "bolt-01", 2),
                TestContext.Current.CancellationToken
            )
        );
        Assert.IsType<GetStockPositionResult.Found>(
            await positions.GetAsOfAsync(
                new(
                    organization.UserId,
                    organization.OrganizationId,
                    "main",
                    "bolt-01",
                    recordedAt
                ),
                TestContext.Current.CancellationToken
            )
        );
        Assert.IsType<GetStockPositionHistoryResult.Found>(
            await positions.GetHistoryAsync(
                new(organization.UserId, organization.OrganizationId, "main", "bolt-01"),
                TestContext.Current.CancellationToken
            )
        );
        Assert.Equal(2, await CountStoredEventsAsync(postgres.GetConnectionString()));
    }

    [Fact]
    public async Task RecordReceipt_RecordedClockMovesBackwards_RejectsAppendWithoutChangingState()
    {
        await using PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:18.6").Build();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        OrganizationAccessContext organization = CreateOrganizationContext();
        var clock = new HistoryTimeProvider(new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero));
        await using ServiceProvider services = await CreateServicesAsync(
            postgres.GetConnectionString(),
            organization,
            timeProvider: clock
        );
        await CreateReferenceDataAsync(services, organization);
        Assert.IsType<RecordStockReceiptResult.Recorded>(
            await RecordReceiptAsync(services, organization, quantity: 10m, expectedVersion: 0)
        );
        clock.UtcNow = clock.UtcNow.AddSeconds(-1);

        StockPositionIntegrityException failure =
            await Assert.ThrowsAsync<StockPositionIntegrityException>(() =>
                RecordReceiptAsync(services, organization, quantity: 2m, expectedVersion: 2)
            );
        Assert.Equal(StockPositionIntegrityFailure.RecordedTimeRegression, failure.Failure);
        Assert.Equal(2, await CountStoredEventsAsync(postgres.GetConnectionString()));
    }

    [Fact]
    public async Task GetHistory_CursorPages_ReturnsOrderedBusinessFactsWithoutRepeatingEntries()
    {
        await using PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:18.6").Build();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        OrganizationAccessContext organization = CreateOrganizationContext();
        DateTimeOffset recordedAt = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);
        await using ServiceProvider services = await CreateServicesAsync(
            postgres.GetConnectionString(),
            organization,
            timeProvider: new HistoryTimeProvider(recordedAt)
        );
        await CreateReferenceDataAsync(services, organization);
        Assert.IsType<RecordStockReceiptResult.Recorded>(
            await RecordReceiptAsync(services, organization, quantity: 10m, expectedVersion: 0)
        );
        Assert.IsType<RecordStockReceiptResult.Recorded>(
            await RecordReceiptAsync(services, organization, quantity: 2m, expectedVersion: 2)
        );
        Assert.IsType<RecordStockReceiptResult.Recorded>(
            await RecordReceiptAsync(services, organization, quantity: 3m, expectedVersion: 3)
        );

        await using AsyncServiceScope scope = services.CreateAsyncScope();
        IStockPositionOperations positions =
            scope.ServiceProvider.GetRequiredService<IStockPositionOperations>();
        var query = new GetStockPositionHistoryQuery(
            organization.UserId,
            organization.OrganizationId,
            "main",
            "bolt-01",
            Limit: 2
        );
        StockPositionHistoryView first = Assert
            .IsType<GetStockPositionHistoryResult.Found>(
                await positions.GetHistoryAsync(query, TestContext.Current.CancellationToken)
            )
            .History;
        Assert.Equal(4, first.Version);
        Assert.Equal("EA", first.BaseUnitCode);
        Assert.Equal([1L, 2L], first.Entries.Select(entry => entry.Version));
        Assert.Equal(StockPositionHistoryAction.Opened, first.Entries[0].Action);
        Assert.Null(first.Entries[0].Quantity);
        Assert.Equal(StockPositionHistoryAction.Received, first.Entries[1].Action);
        Assert.Equal(10m, first.Entries[1].Quantity);
        Assert.All(first.Entries, entry => Assert.Equal(recordedAt, entry.RecordedAt));
        Assert.Equal(2, first.NextAfterVersion);

        StockPositionHistoryView last = Assert
            .IsType<GetStockPositionHistoryResult.Found>(
                await positions.GetHistoryAsync(
                    query with
                    {
                        AfterVersion = first.NextAfterVersion!.Value,
                    },
                    TestContext.Current.CancellationToken
                )
            )
            .History;
        Assert.Equal([3L, 4L], last.Entries.Select(entry => entry.Version));
        Assert.Equal([2m, 3m], last.Entries.Select(entry => entry.Quantity!.Value));
        Assert.Null(last.NextAfterVersion);
    }

    private static async Task ExecuteHistorySqlAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private sealed class HistoryTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        internal DateTimeOffset UtcNow { get; set; } = utcNow;

        public override DateTimeOffset GetUtcNow() => UtcNow;
    }
}
