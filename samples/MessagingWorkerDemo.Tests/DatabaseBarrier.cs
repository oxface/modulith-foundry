using Npgsql;

namespace ModulithFoundry.Samples.MessagingWorkerDemo.Tests;

// Hold an uncommitted job INSERT, or a completion UPDATE after confirmed publication.
// These disposable-database triggers replace timing guesses and production fault switches.
internal sealed class DatabaseBarrier(NpgsqlConnection owner, string database) : IAsyncDisposable
{
    private const int LockKey = 100919;
    private bool released;

    internal static async Task<DatabaseBarrier> HoldAsync(
        string database,
        bool publicationCompletion,
        CancellationToken cancellation
    )
    {
        var owner = new NpgsqlConnection(database);
        await owner.OpenAsync(cancellation);
        try
        {
            await using var command = new NpgsqlCommand(
                $"""
                SELECT pg_advisory_lock({LockKey});
                CREATE FUNCTION public.worker_proof_barrier() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    PERFORM pg_advisory_xact_lock({LockKey});
                    RETURN NEW;
                END;
                $$;
                """
                    + (
                        publicationCompletion
                            ? "CREATE TRIGGER worker_proof_barrier BEFORE UPDATE ON exports.outgoing_work FOR EACH ROW WHEN (NEW.dispatched_at IS NOT NULL) EXECUTE FUNCTION public.worker_proof_barrier();"
                            : "CREATE TRIGGER worker_proof_barrier AFTER INSERT ON rendering.jobs FOR EACH ROW EXECUTE FUNCTION public.worker_proof_barrier();"
                    ),
                owner
            );
            await command.ExecuteNonQueryAsync(cancellation);
            return new(owner, database);
        }
        catch
        {
            await owner.DisposeAsync();
            throw;
        }
    }

    internal Task WaitForBlockedAsync(int count, CancellationToken cancellation) =>
        WorkerTopology.EventuallyAsync(
            async () =>
                await WorkerTopology.SqlCountAsync(
                    database,
                    """
                    SELECT count(*) FROM pg_locks locks JOIN pg_stat_activity activity ON activity.pid = locks.pid
                    WHERE locks.locktype = 'advisory' AND NOT locks.granted
                      AND activity.datname = current_database() AND activity.application_name LIKE 'proof-%'
                    """,
                    cancellation
                ) == count,
            cancellation
        );

    internal async Task ReleaseAsync(CancellationToken cancellation)
    {
        if (released)
            return;

        await using var command = new NpgsqlCommand($"SELECT pg_advisory_unlock({LockKey})", owner);
        await command.ExecuteNonQueryAsync(cancellation);
        released = true;
    }

    public async ValueTask DisposeAsync() => await owner.DisposeAsync();
}
