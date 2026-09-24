using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace ModulithFoundry.Migrator;

internal sealed class PostgresMigrationLock(
    IConfiguration configuration,
    ILogger<PostgresMigrationLock> logger)
{
    private const string LockName = "modulith-foundry:migrations";

    public async Task<Lease> AcquireAsync(CancellationToken cancellationToken)
    {
        string connectionString = configuration.GetConnectionString("database")
            ?? throw new InvalidOperationException("Connection string 'database' is required.");
        var connectionStringBuilder = new NpgsqlConnectionStringBuilder(connectionString)
        {
            Pooling = false,
        };
        var connection = new NpgsqlConnection(connectionStringBuilder.ConnectionString);

        try
        {
            await connection.OpenAsync(cancellationToken);
            await using var command = new NpgsqlCommand(
                "SELECT pg_advisory_lock(hashtextextended($1, 0));",
                connection);
            command.Parameters.AddWithValue(LockName);

            MigrationLogs.WaitingForLock(logger);
            await command.ExecuteNonQueryAsync(cancellationToken);
            MigrationLogs.LockAcquired(logger);
            return new Lease(connection, logger);
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    internal sealed class Lease(
        NpgsqlConnection connection,
        ILogger logger) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            try
            {
                await using var command = new NpgsqlCommand(
                    "SELECT pg_advisory_unlock(hashtextextended($1, 0));",
                    connection);
                command.Parameters.AddWithValue(LockName);

                await command.ExecuteNonQueryAsync();
                MigrationLogs.LockReleased(logger);
            }
            finally
            {
                await connection.DisposeAsync();
            }
        }
    }
}
