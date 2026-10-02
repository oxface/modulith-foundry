using Npgsql;

namespace ModulithFoundry.BrokerTests;

// Disposable database synchronization only; product assertions remain at Contracts/broker seams.
internal sealed class ReceiverDatabaseBarrier : IAsyncDisposable
{
    private const long LockKey = 512345678;
    private readonly NpgsqlConnection connection;
    private readonly CancellationToken cancellationToken;
    private readonly string schema;
    private readonly string table;

    private ReceiverDatabaseBarrier(
        NpgsqlConnection connection,
        string schema,
        string table,
        CancellationToken cancellationToken
    )
    {
        this.connection = connection;
        this.schema = schema;
        this.table = table;
        this.cancellationToken = cancellationToken;
    }

    internal static async Task<ReceiverDatabaseBarrier> CreateAsync(
        ReservationFixture fixture,
        bool dispatchMark
    ) =>
        await CreateAsync(
            fixture.DatabaseConnectionString,
            "inventory",
            dispatchMark ? "outbox_messages" : "inbox_receipts",
            dispatchMark ? "UPDATE" : "INSERT",
            dispatchMark ? "IF NEW.dispatched_at IS NOT NULL THEN" : "",
            fixture.CancellationToken
        );

    internal static Task<ReceiverDatabaseBarrier> CreatePurchasingAsync(
        StockItemBootstrapFixture fixture,
        bool installation
    ) =>
        CreateAsync(
            fixture.DatabaseConnectionString,
            "purchasing",
            installation ? "stock_item_bootstrap" : "stock_item_reference_inbox",
            installation ? "UPDATE" : "INSERT",
            "",
            fixture.CancellationToken
        );

    internal static Task<ReceiverDatabaseBarrier> CreateReleaseDispatchAsync(
        ReservationFixture fixture
    ) =>
        CreateAsync(
            fixture.DatabaseConnectionString,
            "inventory",
            "outbox_messages",
            "UPDATE",
            "IF NEW.dispatched_at IS NOT NULL AND NEW.message_type = 'inventory.reservation-release-outcome.v1' THEN",
            fixture.CancellationToken
        );

    internal static Task<ReceiverDatabaseBarrier> CreateSalesAsync(
        SalesFulfilmentFixture fixture
    ) =>
        CreateAsync(
            fixture.DatabaseConnectionString,
            "sales",
            "inbox_receipts",
            "INSERT",
            "",
            fixture.CancellationToken
        );

    private static async Task<ReceiverDatabaseBarrier> CreateAsync(
        string databaseConnection,
        string schema,
        string table,
        string operation,
        string guard,
        CancellationToken cancellationToken
    )
    {
        var connection = new NpgsqlConnection(databaseConnection);
        await connection.OpenAsync(cancellationToken);
        var barrier = new ReceiverDatabaseBarrier(connection, schema, table, cancellationToken);
        try
        {
            string endGuard = guard.Length > 0 ? "END IF;" : "";
            await using var command = new NpgsqlCommand(
                $"""
                SELECT pg_advisory_lock({LockKey});
                CREATE FUNCTION {schema}.receiver_test_barrier() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    {guard}
                    PERFORM pg_advisory_xact_lock({LockKey});
                    {endGuard}
                    RETURN NEW;
                END;
                $$;
                CREATE TRIGGER receiver_test_barrier BEFORE {operation} ON {schema}.{table}
                FOR EACH ROW EXECUTE FUNCTION {schema}.receiver_test_barrier();
                """,
                connection
            );
            await command.ExecuteNonQueryAsync(cancellationToken);
            return barrier;
        }
        catch
        {
            await barrier.DisposeAsync();
            throw;
        }
    }

    internal async Task WaitAsync(ReceiverProcess child)
    {
        await using var command = new NpgsqlCommand(
            """
            SELECT EXISTS (
                SELECT 1 FROM pg_stat_activity
                WHERE application_name = @application AND wait_event_type = 'Lock' AND wait_event = 'advisory'
            );
            """,
            connection
        );
        command.Parameters.AddWithValue("application", child.ApplicationName);
        while (!(bool)(await command.ExecuteScalarAsync(cancellationToken))!)
            await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken);
    }

    internal async Task WaitForDisconnectAsync(ReceiverProcess child)
    {
        await using var command = new NpgsqlCommand(
            "SELECT EXISTS (SELECT 1 FROM pg_stat_activity WHERE application_name = @application);",
            connection
        );
        command.Parameters.AddWithValue("application", child.ApplicationName);
        while ((bool)(await command.ExecuteScalarAsync(cancellationToken))!)
            await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            // The test kills its blocked child before disposal. Cleanup has an independent bound.
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await using var command = new NpgsqlCommand(
                $"""
                SELECT pg_advisory_unlock_all();
                DROP TRIGGER IF EXISTS receiver_test_barrier ON {schema}.{table};
                DROP FUNCTION IF EXISTS {schema}.receiver_test_barrier();
                """,
                connection
            );
            await command.ExecuteNonQueryAsync(timeout.Token);
        }
        finally
        {
            await connection.DisposeAsync();
        }
    }
}
