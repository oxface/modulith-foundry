using Npgsql;

namespace ModulithFoundry.BrokerTests;

// Disposable database synchronization only; product assertions remain at Contracts/broker seams.
internal sealed class ReceiverDatabaseBarrier : IAsyncDisposable
{
    private const long LockKey = 512345678;
    private readonly NpgsqlConnection connection;
    private readonly CancellationToken cancellationToken;

    private ReceiverDatabaseBarrier(
        NpgsqlConnection connection,
        CancellationToken cancellationToken
    )
    {
        this.connection = connection;
        this.cancellationToken = cancellationToken;
    }

    internal static async Task<ReceiverDatabaseBarrier> CreateAsync(
        ReservationFixture fixture,
        bool dispatchMark
    )
    {
        var connection = new NpgsqlConnection(fixture.DatabaseConnectionString);
        await connection.OpenAsync(fixture.CancellationToken);
        var barrier = new ReceiverDatabaseBarrier(connection, fixture.CancellationToken);
        try
        {
            string guard = dispatchMark ? "IF NEW.dispatched_at IS NOT NULL THEN" : "";
            string endGuard = dispatchMark ? "END IF;" : "";
            string table = dispatchMark ? "outbox_messages" : "inbox_receipts";
            string operation = dispatchMark ? "UPDATE" : "INSERT";
            await using var command = new NpgsqlCommand(
                $"""
                SELECT pg_advisory_lock({LockKey});
                CREATE FUNCTION inventory.receiver_test_barrier() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    {guard}
                    PERFORM pg_advisory_xact_lock({LockKey});
                    {endGuard}
                    RETURN NEW;
                END;
                $$;
                CREATE TRIGGER receiver_test_barrier BEFORE {operation} ON inventory.{table}
                FOR EACH ROW EXECUTE FUNCTION inventory.receiver_test_barrier();
                """,
                connection
            );
            await command.ExecuteNonQueryAsync(fixture.CancellationToken);
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
                """
                SELECT pg_advisory_unlock_all();
                DROP TRIGGER IF EXISTS receiver_test_barrier ON inventory.inbox_receipts;
                DROP TRIGGER IF EXISTS receiver_test_barrier ON inventory.outbox_messages;
                DROP FUNCTION IF EXISTS inventory.receiver_test_barrier();
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
