using Microsoft.EntityFrameworkCore;
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
    public async Task Rebuild_CorruptCurrentModel_RepairsFromFullHistoryWithoutChangingBusinessHistory()
    {
        await using PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:18.6").Build();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        OrganizationAccessContext organization = CreateOrganizationContext();
        await using ServiceProvider services = await CreateServicesAsync(postgres.GetConnectionString(), organization);
        await CreateReferenceDataAsync(services, organization);
        StockPositionView position = Assert.IsType<RecordStockReceiptResult.Recorded>(
            await RecordReceiptAsync(services, organization, 10m, expectedVersion: 0)).Position;
        await CorrectQuantityAsync(services, organization, 7m, "Count confirmed", expectedVersion: 2);
        await ExecuteHistorySqlAsync(postgres.GetConnectionString(),
            "UPDATE inventory.stock_position_current SET on_hand_quantity = 99, available_quantity = 99");

        // This repair identity cannot append/change events or rewrite old audits.
        // The trigger also rejects replay of business audit actions.
        await ExecuteHistorySqlAsync(postgres.GetConnectionString(), """
            CREATE ROLE projection_replayer LOGIN PASSWORD 'test-only-replayer';
            GRANT USAGE ON SCHEMA inventory TO projection_replayer;
            GRANT SELECT ON ALL TABLES IN SCHEMA inventory TO projection_replayer;
            GRANT UPDATE ON inventory.stock_position_current TO projection_replayer;
            GRANT INSERT ON inventory.audit_entries TO projection_replayer;
            CREATE FUNCTION inventory.only_rebuild_audit() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                IF NEW.action <> 'stock-position.projection-rebuilt' THEN
                    RAISE EXCEPTION 'replayed business audit';
                END IF;
                RETURN NEW;
            END; $$;
            CREATE TRIGGER only_rebuild_audit BEFORE INSERT ON inventory.audit_entries
            FOR EACH ROW EXECUTE FUNCTION inventory.only_rebuild_audit();
            """);
        string replayConnection = new NpgsqlConnectionStringBuilder(postgres.GetConnectionString())
        {
            Username = "projection_replayer",
            Password = "test-only-replayer",
        }.ConnectionString;
        await using ServiceProvider replayer = await CreateServicesAsync(replayConnection, organization, migrate: false);
        StockPositionRebuildResult.Rebuilt rebuilt = Assert.IsType<StockPositionRebuildResult.Rebuilt>(
            await RebuildProjectionAsync(replayer, organization, position.StockPositionId));
        Assert.False(rebuilt.PreviousModelMatched);
        Assert.Equal(3, rebuilt.Version);
        Assert.True(Assert.IsType<StockPositionRebuildResult.Rebuilt>(
            await RebuildProjectionAsync(replayer, organization, position.StockPositionId)).PreviousModelMatched);
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        StockPositionView repaired = await ReadCurrentPositionAsync(services, organization);
        Assert.Equal(7m, repaired.OnHandQuantity);
        Assert.Equal(3, repaired.Version);
        StockPositionHistoryView history = Assert.IsType<GetStockPositionHistoryResult.Found>(await scope.ServiceProvider
            .GetRequiredService<IStockPositionOperations>().GetHistoryAsync(
                new(organization.UserId, organization.OrganizationId, "main", "bolt-01"), TestContext.Current.CancellationToken)).History;
        Assert.Equal(3, history.Entries.Count);
        Assert.Equal("Count confirmed", history.Entries[^1].Reason);
    }

    [Fact]
    public async Task Rebuild_WriterAlreadyLoaded_WaitsThenCapturesCommittedHead()
    {
        await using PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:18.6").Build();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        OrganizationAccessContext organization = CreateOrganizationContext();
        await using ServiceProvider services = await CreateServicesAsync(postgres.GetConnectionString(), organization);
        await CreateReferenceDataAsync(services, organization);
        StockPositionView position = Assert.IsType<RecordStockReceiptResult.Recorded>(
            await RecordReceiptAsync(services, organization, 10m, expectedVersion: 0)).Position;
        await InstallProjectionPauseAsync(postgres.GetConnectionString());
        await using var blocker = new NpgsqlConnection(postgres.GetConnectionString());
        await blocker.OpenAsync(TestContext.Current.CancellationToken);
        await using NpgsqlTransaction blockingTransaction = await blocker.BeginTransactionAsync(TestContext.Current.CancellationToken);
        await using var blockCommand = new NpgsqlCommand("SELECT pg_advisory_xact_lock(731025)", blocker, blockingTransaction);
        await blockCommand.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        await using ServiceProvider writer = await CreateNamedServicesAsync(postgres.GetConnectionString(), organization, "active-stock-writer");
        await using ServiceProvider promoter = await CreateNamedServicesAsync(postgres.GetConnectionString(), organization, "waiting-promoter");
        Task<RecordStockReceiptResult> append = RecordReceiptAsync(writer, organization, 2m, expectedVersion: 2);
        await WaitForAdvisoryWaitAsync(postgres.GetConnectionString(), "active-stock-writer");
        Task<StockPositionRebuildResult> swap = RebuildProjectionAsync(promoter, organization, position.StockPositionId);
        await WaitForAdvisoryWaitAsync(postgres.GetConnectionString(), "waiting-promoter");
        await blockingTransaction.CommitAsync(TestContext.Current.CancellationToken);
        StockPositionView winner = Assert.IsType<RecordStockReceiptResult.Recorded>(await append).Position;
        Assert.Equal(3, Assert.IsType<StockPositionRebuildResult.Rebuilt>(await swap).Version);
        Assert.Equal(12m, winner.OnHandQuantity);
        Assert.Equal(winner, await ReadCurrentPositionAsync(services, organization));
    }

    [Fact]
    public async Task Rebuild_InProgress_WriterLoadsOnlyAfterRepairCommits()
    {
        await using PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:18.6").Build();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        OrganizationAccessContext organization = CreateOrganizationContext();
        await using ServiceProvider services = await CreateServicesAsync(postgres.GetConnectionString(), organization);
        await CreateReferenceDataAsync(services, organization);
        StockPositionView position = Assert.IsType<RecordStockReceiptResult.Recorded>(
            await RecordReceiptAsync(services, organization, 10m, expectedVersion: 0)).Position;
        await ExecuteHistorySqlAsync(postgres.GetConnectionString(),
            "UPDATE inventory.stock_position_current SET on_hand_quantity = 99, available_quantity = 99");
        await InstallProjectionPauseAsync(postgres.GetConnectionString());
        await using var blocker = new NpgsqlConnection(postgres.GetConnectionString());
        await blocker.OpenAsync(TestContext.Current.CancellationToken);
        await using NpgsqlTransaction blockingTransaction = await blocker.BeginTransactionAsync(TestContext.Current.CancellationToken);
        await using var blockCommand = new NpgsqlCommand("SELECT pg_advisory_xact_lock(731025)", blocker, blockingTransaction);
        await blockCommand.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        await using ServiceProvider promoter = await CreateNamedServicesAsync(postgres.GetConnectionString(), organization, "active-promoter");
        await using ServiceProvider writer = await CreateNamedServicesAsync(postgres.GetConnectionString(), organization, "waiting-stock-writer");
        Task<StockPositionRebuildResult> swap = RebuildProjectionAsync(promoter, organization, position.StockPositionId);
        await WaitForAdvisoryWaitAsync(postgres.GetConnectionString(), "active-promoter");
        Task<RecordStockReceiptResult> append = RecordReceiptAsync(writer, organization, 2m, expectedVersion: 2);
        await WaitForAdvisoryWaitAsync(postgres.GetConnectionString(), "waiting-stock-writer");
        await blockingTransaction.CommitAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, Assert.IsType<StockPositionRebuildResult.Rebuilt>(await swap).Version);
        StockPositionView appended = Assert.IsType<RecordStockReceiptResult.Recorded>(await append).Position;
        Assert.Equal(12m, appended.OnHandQuantity);
        Assert.Equal(3, appended.Version);
        Assert.Equal(appended, await ReadCurrentPositionAsync(services, organization));
    }

    [Fact]
    public async Task Rebuild_MissingModel_RestoresLookupFromRetainedStream()
    {
        await using PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:18.6").Build();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        OrganizationAccessContext organization = CreateOrganizationContext();
        await using ServiceProvider services = await CreateServicesAsync(postgres.GetConnectionString(), organization);
        await CreateReferenceDataAsync(services, organization);
        StockPositionView position = Assert.IsType<RecordStockReceiptResult.Recorded>(
            await RecordReceiptAsync(services, organization, 10m, expectedVersion: 0)).Position;
        await ExecuteHistorySqlAsync(postgres.GetConnectionString(), "DELETE FROM inventory.stock_position_current");
        Assert.IsType<RecordStockReceiptResult.VersionConflict>(await RecordReceiptAsync(services, organization, 1m, expectedVersion: 2));
        Assert.False(Assert.IsType<StockPositionRebuildResult.Rebuilt>(
            await RebuildProjectionAsync(services, organization, position.StockPositionId)).PreviousModelMatched);
        Assert.Equal(position, await ReadCurrentPositionAsync(services, organization));
        StockPositionView appended = Assert.IsType<RecordStockReceiptResult.Recorded>(
            await RecordReceiptAsync(services, organization, 1m, expectedVersion: 2)).Position;
        Assert.Equal(position.StockPositionId, appended.StockPositionId);
        Assert.Equal(11m, appended.OnHandQuantity);
    }

    [Theory]
    [InlineData("UPDATE inventory.events SET event_name = 'unknown-fact' WHERE stream_version = 2", "UnknownEvent")]
    [InlineData("UPDATE inventory.events SET payload = '{\"quantity\":\"unreadable\"}' WHERE stream_version = 2", "InvalidEventPayload")]
    [InlineData("DELETE FROM inventory.events WHERE stream_version = 2", "HistoryVersionMismatch")]
    public async Task Rebuild_UnreadableHistory_LeavesServingModelUntouched(string faultSql, string expectedFailure)
    {
        await using PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:18.6").Build();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        OrganizationAccessContext organization = CreateOrganizationContext();
        await using ServiceProvider services = await CreateServicesAsync(postgres.GetConnectionString(), organization);
        await CreateReferenceDataAsync(services, organization);
        StockPositionView position = Assert.IsType<RecordStockReceiptResult.Recorded>(
            await RecordReceiptAsync(services, organization, 10m, expectedVersion: 0)).Position;
        await ExecuteHistorySqlAsync(postgres.GetConnectionString(), faultSql);
        StockPositionIntegrityException failure = await Assert.ThrowsAsync<StockPositionIntegrityException>(() =>
            RebuildProjectionAsync(services, organization, position.StockPositionId));
        Assert.Equal(expectedFailure, failure.Failure.ToString());
        Assert.Equal(position.StockPositionId.Value, failure.StreamId);
        Assert.Equal(position, await ReadCurrentPositionAsync(services, organization));
    }

    [Fact]
    public async Task Rebuild_WriteFails_PreservesServingModelAndRetriesFullHistoryInFreshScope()
    {
        await using PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:18.6").Build();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        OrganizationAccessContext organization = CreateOrganizationContext();
        await using ServiceProvider services = await CreateServicesAsync(postgres.GetConnectionString(), organization);
        await CreateReferenceDataAsync(services, organization);
        StockPositionView position = Assert.IsType<RecordStockReceiptResult.Recorded>(
            await RecordReceiptAsync(services, organization, 10m, expectedVersion: 0)).Position;
        await ExecuteHistorySqlAsync(postgres.GetConnectionString(),
            "UPDATE inventory.stock_position_current SET on_hand_quantity = 99, available_quantity = 99");
        await CreateProjectionFailureTriggerAsync(postgres.GetConnectionString());
        await Assert.ThrowsAsync<DbUpdateException>(() => RebuildProjectionAsync(services, organization, position.StockPositionId));
        Assert.Equal(99m, (await ReadCurrentPositionAsync(services, organization)).OnHandQuantity);
        await ExecuteHistorySqlAsync(postgres.GetConnectionString(),
            "DROP TRIGGER reject_stock_position_update ON inventory.stock_position_current");
        await using ServiceProvider restarted = await CreateServicesAsync(postgres.GetConnectionString(), organization, migrate: false);
        Assert.IsType<StockPositionRebuildResult.Rebuilt>(await RebuildProjectionAsync(restarted, organization, position.StockPositionId));
        Assert.Equal(position, await ReadCurrentPositionAsync(restarted, organization));
    }

    [Fact]
    public async Task Rebuild_CancelledDuringReplacement_RollsBackAndRetriesFromBeginning()
    {
        await using PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:18.6").Build();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        OrganizationAccessContext organization = CreateOrganizationContext();
        string connectionString = new NpgsqlConnectionStringBuilder(postgres.GetConnectionString())
        {
            ApplicationName = "cancelled-rebuilder",
        }.ConnectionString;
        await using ServiceProvider services = await CreateServicesAsync(connectionString, organization);
        await CreateReferenceDataAsync(services, organization);
        StockPositionView position = Assert.IsType<RecordStockReceiptResult.Recorded>(
            await RecordReceiptAsync(services, organization, 10m, expectedVersion: 0)).Position;
        await RecordReceiptAsync(services, organization, 2m, expectedVersion: 2);
        await ExecuteHistorySqlAsync(postgres.GetConnectionString(),
            "UPDATE inventory.stock_position_current SET on_hand_quantity = 99, available_quantity = 99");
        await InstallProjectionPauseAsync(postgres.GetConnectionString());
        await using var blocker = new NpgsqlConnection(postgres.GetConnectionString());
        await blocker.OpenAsync(TestContext.Current.CancellationToken);
        await using NpgsqlTransaction blockingTransaction = await blocker.BeginTransactionAsync(TestContext.Current.CancellationToken);
        await using var blockCommand = new NpgsqlCommand("SELECT pg_advisory_xact_lock(731025)", blocker, blockingTransaction);
        await blockCommand.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        Task<StockPositionRebuildResult> cancelled = RebuildProjectionAsync(services, organization, position.StockPositionId, cancellation.Token);
        await WaitForAdvisoryWaitAsync(postgres.GetConnectionString(), "cancelled-rebuilder");
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled);
        await blockingTransaction.RollbackAsync(TestContext.Current.CancellationToken);
        Assert.Equal(99m, (await ReadCurrentPositionAsync(services, organization)).OnHandQuantity);
        await ExecuteHistorySqlAsync(postgres.GetConnectionString(),
            "DROP TRIGGER pause_projection_update ON inventory.stock_position_current");
        await using ServiceProvider restarted = await CreateServicesAsync(postgres.GetConnectionString(), organization, migrate: false);
        Assert.Equal(3, Assert.IsType<StockPositionRebuildResult.Rebuilt>(
            await RebuildProjectionAsync(restarted, organization, position.StockPositionId)).Version);
        Assert.Equal(12m, (await ReadCurrentPositionAsync(restarted, organization)).OnHandQuantity);
    }

    [Fact]
    public async Task Rebuild_ForeignContextOrRevokedPermission_DoesNotRepair()
    {
        await using PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:18.6").Build();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        OrganizationAccessContext organization = CreateOrganizationContext();
        await using ServiceProvider services = await CreateServicesAsync(postgres.GetConnectionString(), organization);
        await CreateReferenceDataAsync(services, organization);
        StockPositionView position = Assert.IsType<RecordStockReceiptResult.Recorded>(
            await RecordReceiptAsync(services, organization, 10m, expectedVersion: 0)).Position;
        OrganizationAccessContext foreign = CreateOrganizationContext();
        Assert.IsType<StockPositionRebuildResult.PermissionDenied>(
            await RebuildProjectionAsync(services, foreign, position.StockPositionId));
        SetOrganizationContext(services, foreign);
        Assert.IsType<StockPositionRebuildResult.NotFound>(
            await RebuildProjectionAsync(services, foreign, position.StockPositionId));
        SetOrganizationContext(services, organization);
        services.GetRequiredService<TestOrganizationAuthorization>().Allow = false;
        Assert.IsType<StockPositionRebuildResult.PermissionDenied>(
            await RebuildProjectionAsync(services, organization, position.StockPositionId));
        services.GetRequiredService<TestOrganizationAuthorization>().Allow = true;
        Assert.Equal(position, await ReadCurrentPositionAsync(services, organization));
    }

    private static async Task<StockPositionRebuildResult> RebuildProjectionAsync(
        ServiceProvider services, OrganizationAccessContext organization, StockPositionId positionId, CancellationToken? cancellationToken = null)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IStockPositionProjectionRebuilder>().RebuildAsync(
            new(organization.UserId, organization.OrganizationId, positionId), cancellationToken ?? TestContext.Current.CancellationToken);
    }

    private static async Task<StockPositionView> ReadCurrentPositionAsync(ServiceProvider services, OrganizationAccessContext organization)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        return Assert.IsType<GetStockPositionResult.Found>(await scope.ServiceProvider
            .GetRequiredService<IStockPositionOperations>().GetCurrentAsync(
            new(organization.UserId, organization.OrganizationId, "main", "bolt-01"), TestContext.Current.CancellationToken)).Position;
    }
    // SQL synchronization is fault setup only; all assertions below use the module Contracts.
    private static async Task WaitForAdvisoryWaitAsync(string connectionString, string applicationName)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(timeout.Token);
        await using var command = new NpgsqlCommand(
            "SELECT EXISTS (SELECT 1 FROM pg_stat_activity WHERE application_name = @name AND wait_event = 'advisory')", connection);
        command.Parameters.AddWithValue("name", applicationName);
        while (!(bool)(await command.ExecuteScalarAsync(timeout.Token))!)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(20), timeout.Token);
        }
    }

    private static Task InstallProjectionPauseAsync(string connectionString) => ExecuteHistorySqlAsync(connectionString, """
        CREATE FUNCTION inventory.pause_projection_update() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN PERFORM pg_advisory_xact_lock(731025); RETURN NEW; END; $$;
        CREATE TRIGGER pause_projection_update BEFORE UPDATE ON inventory.stock_position_current
        FOR EACH ROW EXECUTE FUNCTION inventory.pause_projection_update();
        """);

    private static Task<ServiceProvider> CreateNamedServicesAsync(string connectionString, OrganizationAccessContext organization, string name) =>
        CreateServicesAsync(new NpgsqlConnectionStringBuilder(connectionString) { ApplicationName = name }.ConnectionString, organization, migrate: false);
}
