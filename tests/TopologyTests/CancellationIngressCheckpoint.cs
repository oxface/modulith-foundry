using Npgsql;

namespace ModulithFoundry.TopologyTests;

// Test coordination only: block an uncommitted cancellation or observe its commit notification.
internal sealed class CancellationIngressCheckpoint : IAsyncDisposable
{
    private const long LockKey = 856893143;
    private readonly NpgsqlConnection connection;
    private bool committed;

    private CancellationIngressCheckpoint(NpgsqlConnection connection)
    {
        this.connection = connection;
        connection.Notification += (_, notification) =>
            committed |= notification.Channel == "cancellation_ingress_test";
    }

    internal static async Task<CancellationIngressCheckpoint> CreateAsync(
        string connectionString,
        bool afterCommit,
        CancellationToken cancellationToken
    )
    {
        var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        var checkpoint = new CancellationIngressCheckpoint(connection);
        try
        {
            await using var command = new NpgsqlCommand(
                $"""
                LISTEN cancellation_ingress_test;
                SELECT pg_advisory_lock({LockKey});
                CREATE FUNCTION sales.cancellation_ingress_test() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF OLD.status <> 'cancelled' AND NEW.status = 'cancelled' THEN
                        {(afterCommit ? "" : $"PERFORM pg_advisory_xact_lock({LockKey});")}
                        PERFORM pg_notify('cancellation_ingress_test', 'committed');
                    END IF;
                    RETURN NEW;
                END $$;
                CREATE TRIGGER cancellation_ingress_test BEFORE UPDATE ON sales.orders
                FOR EACH ROW EXECUTE FUNCTION sales.cancellation_ingress_test();
                """,
                connection
            );
            await command.ExecuteNonQueryAsync(cancellationToken);
            return checkpoint;
        }
        catch
        {
            await checkpoint.DisposeAsync();
            throw;
        }
    }

    internal async Task WaitAsync(bool afterCommit, CancellationToken cancellationToken)
    {
        if (afterCommit)
        {
            // PostgreSQL delivers NOTIFY only once the transaction commits.
            while (!committed)
                await connection.WaitAsync(cancellationToken);
            return;
        }
        await using var command = new NpgsqlCommand(
            """
            SELECT EXISTS (
                SELECT 1 FROM pg_stat_activity
                WHERE wait_event_type = 'Lock' AND wait_event = 'advisory'
                AND query LIKE '%UPDATE sales.orders%'
            );
            """,
            connection
        );
        while (!(bool)(await command.ExecuteScalarAsync(cancellationToken))!)
            await Task.Delay(50, cancellationToken);
    }

    internal async Task WaitForBlockedClientExitAsync(CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            SELECT EXISTS (
                SELECT 1 FROM pg_stat_activity
                WHERE wait_event_type = 'Lock' AND wait_event = 'advisory'
                AND query LIKE '%UPDATE sales.orders%'
            );
            """,
            connection
        );
        while ((bool)(await command.ExecuteScalarAsync(cancellationToken))!)
            await Task.Delay(50, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try
        {
            await using var command = new NpgsqlCommand(
                """
                SELECT pg_advisory_unlock_all();
                DROP TRIGGER IF EXISTS cancellation_ingress_test ON sales.orders;
                DROP FUNCTION IF EXISTS sales.cancellation_ingress_test();
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
