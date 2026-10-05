using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace ModulithFoundry.Samples.Wholesale.HttpIdentityDemo;

// Readiness is sample schema/connectivity policy. It never queries tenant business rows or repairs schema.
internal sealed class WholesaleDatabaseHealthCheck(IConfiguration configuration) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default
    )
    {
        await using var connection = new NpgsqlConnection(
            configuration.GetConnectionString("Access")
        );
        try
        {
            await connection.OpenAsync(cancellationToken);
            await using var command = new NpgsqlCommand(
                """
                SELECT to_regclass('access.users') IS NOT NULL
                    AND to_regclass('access.external_identities') IS NOT NULL
                    AND to_regclass('access.organizations') IS NOT NULL
                    AND to_regclass('access.memberships') IS NOT NULL
                    AND to_regclass('inventory.stock_availability') IS NOT NULL
                    AND to_regclass('sales.customer_profiles') IS NOT NULL
                    AND to_regclass('sales.customer_addresses') IS NOT NULL
                """,
                connection
            );
            return Equals(true, await command.ExecuteScalarAsync(cancellationToken))
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("Required Wholesale tables are missing.");
        }
        catch (NpgsqlException exception)
        {
            return HealthCheckResult.Unhealthy("The Wholesale database is unavailable.", exception);
        }
    }
}
