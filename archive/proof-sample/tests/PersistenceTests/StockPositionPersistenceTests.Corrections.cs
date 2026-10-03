using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ModulithFoundry.Modules.Access.Contracts;
using ModulithFoundry.Modules.Inventory.Contracts;
using Npgsql;
using Testcontainers.PostgreSql;

namespace ModulithFoundry.PersistenceTests;

public sealed partial class StockPositionPersistenceTests
{
    [Fact]
    public async Task CorrectQuantity_ExistingPosition_PreservesEarlierStateAndAppendsReasonedCorrection()
    {
        await using PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:18.6").Build();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        OrganizationAccessContext organization = CreateOrganizationContext();
        await using ServiceProvider services = await CreateServicesAsync(
            postgres.GetConnectionString(),
            organization
        );
        await CreateReferenceDataAsync(services, organization);
        StockPositionView original = Assert
            .IsType<RecordStockReceiptResult.Recorded>(
                await RecordReceiptAsync(services, organization, 10m, expectedVersion: 0)
            )
            .Position;

        await ExecuteHistorySqlAsync(
            postgres.GetConnectionString(),
            """
            CREATE ROLE correction_writer LOGIN PASSWORD 'test-only-correction';
            GRANT USAGE ON SCHEMA inventory TO correction_writer;
            GRANT SELECT, INSERT, UPDATE ON ALL TABLES IN SCHEMA inventory TO correction_writer;
            GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA inventory TO correction_writer;
            REVOKE UPDATE, DELETE ON inventory.events FROM correction_writer;
            """
        );
        var writerConnection = new NpgsqlConnectionStringBuilder(postgres.GetConnectionString())
        {
            Username = "correction_writer",
            Password = "test-only-correction",
        };
        await using ServiceProvider writer = await CreateServicesAsync(
            writerConnection.ConnectionString,
            organization,
            migrate: false
        );

        StockPositionView corrected = Assert
            .IsType<CorrectStockQuantityResult.Corrected>(
                await CorrectQuantityAsync(
                    writer,
                    organization,
                    7.5m,
                    " Count confirmed ",
                    expectedVersion: 2
                )
            )
            .Position;

        Assert.Equal(original.StockPositionId, corrected.StockPositionId);
        Assert.Equal(7.5m, corrected.OnHandQuantity);
        Assert.Equal(7.5m, corrected.AvailableQuantity);
        Assert.Equal(3, corrected.Version);
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        IStockPositionOperations positions =
            scope.ServiceProvider.GetRequiredService<IStockPositionOperations>();
        StockPositionView historical = Assert
            .IsType<GetStockPositionResult.Found>(
                await positions.GetAtVersionAsync(
                    new(organization.UserId, organization.OrganizationId, "main", "bolt-01", 2),
                    TestContext.Current.CancellationToken
                )
            )
            .Position;
        Assert.Equal(10m, historical.OnHandQuantity);
        StockPositionView replayed = Assert
            .IsType<GetStockPositionResult.Found>(
                await positions.GetAtVersionAsync(
                    new(organization.UserId, organization.OrganizationId, "main", "bolt-01", 3),
                    TestContext.Current.CancellationToken
                )
            )
            .Position;
        Assert.Equal(corrected, replayed);
        StockPositionHistoryView history = Assert
            .IsType<GetStockPositionHistoryResult.Found>(
                await positions.GetHistoryAsync(
                    new(organization.UserId, organization.OrganizationId, "main", "bolt-01"),
                    TestContext.Current.CancellationToken
                )
            )
            .History;
        Assert.Equal(3, history.Entries.Count);
        StockPositionHistoryEntry correction = history.Entries[^1];
        Assert.Equal(StockPositionHistoryAction.QuantityCorrected, correction.Action);
        Assert.Equal(7.5m, correction.Quantity);
        Assert.Equal("Count confirmed", correction.Reason);
        Assert.All(history.Entries.Take(2), entry => Assert.Null(entry.Reason));
    }

    [Fact]
    public async Task CorrectQuantity_UnchangedValueOrStaleVersion_DoesNotAppendAnotherFact()
    {
        await using PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:18.6").Build();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        OrganizationAccessContext organization = CreateOrganizationContext();
        await using ServiceProvider services = await CreateServicesAsync(
            postgres.GetConnectionString(),
            organization
        );
        await CreateReferenceDataAsync(services, organization);
        await RecordReceiptAsync(services, organization, 10m, expectedVersion: 0);
        StockPositionView unchanged = Assert
            .IsType<CorrectStockQuantityResult.Unchanged>(
                await CorrectQuantityAsync(
                    services,
                    organization,
                    10m,
                    "Count confirmed",
                    expectedVersion: 2
                )
            )
            .Position;
        Assert.Equal(2, unchanged.Version);

        StockPositionView corrected = Assert
            .IsType<CorrectStockQuantityResult.Corrected>(
                await CorrectQuantityAsync(
                    services,
                    organization,
                    0m,
                    "Damaged stock removed",
                    expectedVersion: 2
                )
            )
            .Position;
        Assert.Equal(0m, corrected.AvailableQuantity);
        Assert.Equal(3, corrected.Version);
        Assert.IsType<CorrectStockQuantityResult.VersionConflict>(
            await CorrectQuantityAsync(
                services,
                organization,
                0m,
                "Stale recount",
                expectedVersion: 2
            )
        );
        Assert.IsType<CorrectStockQuantityResult.Unchanged>(
            await CorrectQuantityAsync(
                services,
                organization,
                0m,
                "Count confirmed",
                expectedVersion: 3
            )
        );

        await using AsyncServiceScope scope = services.CreateAsyncScope();
        StockPositionHistoryView history = Assert
            .IsType<GetStockPositionHistoryResult.Found>(
                await scope
                    .ServiceProvider.GetRequiredService<IStockPositionOperations>()
                    .GetHistoryAsync(
                        new(organization.UserId, organization.OrganizationId, "main", "bolt-01"),
                        TestContext.Current.CancellationToken
                    )
            )
            .History;
        Assert.Equal(3, history.Entries.Count);
    }

    [Fact]
    public async Task CorrectQuantity_InvalidQuantityReasonOrVersion_LeavesPositionUnchanged()
    {
        await using PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:18.6").Build();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        OrganizationAccessContext organization = CreateOrganizationContext();
        await using ServiceProvider services = await CreateServicesAsync(
            postgres.GetConnectionString(),
            organization
        );
        await CreateReferenceDataAsync(services, organization);
        await RecordReceiptAsync(services, organization, 10m, expectedVersion: 0);
        (decimal Quantity, string Reason, long Version, string Field)[] invalidCommands =
        [
            (-1m, "Count confirmed", 2, "onHandQuantity"),
            (0.0000001m, "Count confirmed", 2, "onHandQuantity"),
            (10_000_000_000_000m, "Count confirmed", 2, "onHandQuantity"),
            (7m, " ", 2, "reason"),
            (7m, new string('x', 201), 2, "reason"),
            (7m, "Control\ncharacter", 2, "reason"),
            (7m, "Count confirmed", -1, "expectedVersion"),
        ];
        foreach (var invalid in invalidCommands)
        {
            CorrectStockQuantityResult.Invalid result =
                Assert.IsType<CorrectStockQuantityResult.Invalid>(
                    await CorrectQuantityAsync(
                        services,
                        organization,
                        invalid.Quantity,
                        invalid.Reason,
                        invalid.Version
                    )
                );
            Assert.Equal(invalid.Field, result.Field);
        }

        await using AsyncServiceScope scope = services.CreateAsyncScope();
        StockPositionView current = Assert
            .IsType<GetStockPositionResult.Found>(
                await scope
                    .ServiceProvider.GetRequiredService<IStockPositionOperations>()
                    .GetCurrentAsync(
                        new(organization.UserId, organization.OrganizationId, "main", "bolt-01"),
                        TestContext.Current.CancellationToken
                    )
            )
            .Position;
        Assert.Equal(10m, current.OnHandQuantity);
        Assert.Equal(2, current.Version);
    }

    [Fact]
    public async Task CorrectQuantity_UnopenedPosition_DoesNotCreateAStream()
    {
        await using PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:18.6").Build();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        OrganizationAccessContext organization = CreateOrganizationContext();
        await using ServiceProvider services = await CreateServicesAsync(
            postgres.GetConnectionString(),
            organization
        );
        await CreateReferenceDataAsync(services, organization);
        Assert.IsType<CorrectStockQuantityResult.NotFound>(
            await CorrectQuantityAsync(
                services,
                organization,
                7m,
                "Count confirmed",
                expectedVersion: 0
            )
        );
        StockPositionView opened = Assert
            .IsType<RecordStockReceiptResult.Recorded>(
                await RecordReceiptAsync(services, organization, 10m, expectedVersion: 0)
            )
            .Position;
        Assert.Equal(2, opened.Version);
        Assert.Equal(10m, opened.OnHandQuantity);
    }

    [Fact]
    public async Task CorrectQuantity_BelowReservedQuantity_RejectsTheCorrection()
    {
        await using PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:18.6").Build();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        OrganizationAccessContext organization = CreateOrganizationContext();
        await using ServiceProvider services = await CreateServicesAsync(
            postgres.GetConnectionString(),
            organization
        );
        await CreateReferenceDataAsync(services, organization);
        await RecordReceiptAsync(services, organization, 10m, expectedVersion: 0);
        // Reservation commands arrive in Slice 5; seed a valid reserved write state for this invariant.
        await ExecuteHistorySqlAsync(
            postgres.GetConnectionString(),
            "UPDATE inventory.stock_position_current SET reserved_quantity = 4, available_quantity = 6"
        );

        CorrectStockQuantityResult.Invalid result =
            Assert.IsType<CorrectStockQuantityResult.Invalid>(
                await CorrectQuantityAsync(
                    services,
                    organization,
                    3m,
                    "Count confirmed",
                    expectedVersion: 2
                )
            );
        Assert.Equal("onHandQuantity", result.Field);
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        StockPositionView current = Assert
            .IsType<GetStockPositionResult.Found>(
                await scope
                    .ServiceProvider.GetRequiredService<IStockPositionOperations>()
                    .GetCurrentAsync(
                        new(organization.UserId, organization.OrganizationId, "main", "bolt-01"),
                        TestContext.Current.CancellationToken
                    )
            )
            .Position;
        Assert.Equal(10m, current.OnHandQuantity);
        Assert.Equal(4m, current.ReservedQuantity);
        Assert.Equal(2, current.Version);
    }

    [Fact]
    public async Task CorrectQuantity_ConcurrentExpectedVersion_AllowsOnlyOneCorrection()
    {
        await using PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:18.6").Build();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        OrganizationAccessContext organization = CreateOrganizationContext();
        await using ServiceProvider services = await CreateServicesAsync(
            postgres.GetConnectionString(),
            organization
        );
        await CreateReferenceDataAsync(services, organization);
        await RecordReceiptAsync(services, organization, 10m, expectedVersion: 0);

        CorrectStockQuantityResult[] results = await Task.WhenAll(
            CorrectQuantityAsync(services, organization, 7m, "First count", expectedVersion: 2),
            CorrectQuantityAsync(services, organization, 8m, "Second count", expectedVersion: 2)
        );

        Assert.Single(results.OfType<CorrectStockQuantityResult.Corrected>());
        Assert.Single(results.OfType<CorrectStockQuantityResult.VersionConflict>());
        StockPositionView recorded = results
            .OfType<CorrectStockQuantityResult.Corrected>()
            .Single()
            .Position;
        Assert.Equal(3, recorded.Version);
    }

    [Fact]
    public async Task CorrectQuantity_ProjectionWriteFails_RollsBackAndCanRetryAtSameVersion()
    {
        await using PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:18.6").Build();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        OrganizationAccessContext organization = CreateOrganizationContext();
        await using ServiceProvider services = await CreateServicesAsync(
            postgres.GetConnectionString(),
            organization
        );
        await CreateReferenceDataAsync(services, organization);
        await RecordReceiptAsync(services, organization, 10m, expectedVersion: 0);
        await CreateProjectionFailureTriggerAsync(postgres.GetConnectionString());

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            CorrectQuantityAsync(services, organization, 7m, "Count confirmed", expectedVersion: 2)
        );
        await using (AsyncServiceScope scope = services.CreateAsyncScope())
        {
            IStockPositionOperations positions =
                scope.ServiceProvider.GetRequiredService<IStockPositionOperations>();
            Assert.IsType<GetStockPositionResult.NotFound>(
                await positions.GetAtVersionAsync(
                    new(organization.UserId, organization.OrganizationId, "main", "bolt-01", 3),
                    TestContext.Current.CancellationToken
                )
            );
            StockPositionView current = Assert
                .IsType<GetStockPositionResult.Found>(
                    await positions.GetCurrentAsync(
                        new(organization.UserId, organization.OrganizationId, "main", "bolt-01"),
                        TestContext.Current.CancellationToken
                    )
                )
                .Position;
            Assert.Equal(10m, current.OnHandQuantity);
            Assert.Equal(2, current.Version);
        }

        await ExecuteHistorySqlAsync(
            postgres.GetConnectionString(),
            "DROP TRIGGER reject_stock_position_update ON inventory.stock_position_current"
        );
        StockPositionView retried = Assert
            .IsType<CorrectStockQuantityResult.Corrected>(
                await CorrectQuantityAsync(
                    services,
                    organization,
                    7m,
                    "Count confirmed",
                    expectedVersion: 2
                )
            )
            .Position;
        Assert.Equal(3, retried.Version);
    }

    [Fact]
    public async Task CorrectQuantity_ForeignOrganizationOrDeniedPermission_DoesNotChangeOwnedPosition()
    {
        await using PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:18.6").Build();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        OrganizationAccessContext organization = CreateOrganizationContext();
        await using ServiceProvider services = await CreateServicesAsync(
            postgres.GetConnectionString(),
            organization
        );
        await CreateReferenceDataAsync(services, organization);
        await RecordReceiptAsync(services, organization, 10m, expectedVersion: 0);

        OrganizationAccessContext foreign = CreateOrganizationContext();
        Assert.IsType<CorrectStockQuantityResult.PermissionDenied>(
            await CorrectQuantityAsync(services, foreign, 7m, "Count confirmed", expectedVersion: 2)
        );
        SetOrganizationContext(services, foreign);
        await CreateReferenceDataAsync(services, foreign);
        Assert.IsType<CorrectStockQuantityResult.NotFound>(
            await CorrectQuantityAsync(services, foreign, 7m, "Count confirmed", expectedVersion: 0)
        );
        SetOrganizationContext(services, organization);
        services.GetRequiredService<TestOrganizationAuthorization>().Allow = false;
        Assert.IsType<CorrectStockQuantityResult.PermissionDenied>(
            await CorrectQuantityAsync(
                services,
                organization,
                7m,
                "Count confirmed",
                expectedVersion: 2
            )
        );
        services.GetRequiredService<TestOrganizationAuthorization>().Allow = true;
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        StockPositionView current = Assert
            .IsType<GetStockPositionResult.Found>(
                await scope
                    .ServiceProvider.GetRequiredService<IStockPositionOperations>()
                    .GetCurrentAsync(
                        new(organization.UserId, organization.OrganizationId, "main", "bolt-01"),
                        TestContext.Current.CancellationToken
                    )
            )
            .Position;
        Assert.Equal(10m, current.OnHandQuantity);
        Assert.Equal(2, current.Version);
    }

    [Fact]
    public async Task GetHistory_RetainedReasonOutsideCurrentInputRules_PreservesRecordedText()
    {
        await using PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:18.6").Build();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        OrganizationAccessContext organization = CreateOrganizationContext();
        await using ServiceProvider services = await CreateServicesAsync(
            postgres.GetConnectionString(),
            organization
        );
        await CreateReferenceDataAsync(services, organization);
        await RecordReceiptAsync(services, organization, 10m, expectedVersion: 0);
        await CorrectQuantityAsync(
            services,
            organization,
            7m,
            "Count confirmed",
            expectedVersion: 2
        );
        // Simulate retained facts written under different input rules, not a production history edit.
        await ExecuteHistorySqlAsync(
            postgres.GetConnectionString(),
            """
            UPDATE inventory.events
            SET payload = jsonb_set(payload, '{reason}', to_jsonb(' ' || repeat('x', 201) || ' '))
            WHERE stream_version = 3
            """
        );

        await using AsyncServiceScope scope = services.CreateAsyncScope();
        IStockPositionOperations positions =
            scope.ServiceProvider.GetRequiredService<IStockPositionOperations>();
        StockPositionHistoryView history = Assert
            .IsType<GetStockPositionHistoryResult.Found>(
                await positions.GetHistoryAsync(
                    new(organization.UserId, organization.OrganizationId, "main", "bolt-01"),
                    TestContext.Current.CancellationToken
                )
            )
            .History;
        Assert.Equal(" " + new string('x', 201) + " ", history.Entries[^1].Reason);
        StockPositionView replayed = Assert
            .IsType<GetStockPositionResult.Found>(
                await positions.GetAtVersionAsync(
                    new(organization.UserId, organization.OrganizationId, "main", "bolt-01", 3),
                    TestContext.Current.CancellationToken
                )
            )
            .Position;
        Assert.Equal(7m, replayed.OnHandQuantity);
    }

    private static async Task<CorrectStockQuantityResult> CorrectQuantityAsync(
        ServiceProvider services,
        OrganizationAccessContext organization,
        decimal quantity,
        string reason,
        long expectedVersion
    )
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        return await scope
            .ServiceProvider.GetRequiredService<IStockPositionOperations>()
            .CorrectQuantityAsync(
                new(
                    organization.UserId,
                    organization.OrganizationId,
                    "main",
                    "bolt-01",
                    quantity,
                    reason,
                    expectedVersion
                ),
                TestContext.Current.CancellationToken
            );
    }
}
