using Npgsql;

namespace ModulithFoundry.Modules.Sales.Orders.Persistence;

internal sealed class SalesOrderNumberAllocator(NpgsqlDataSource dataSource)
{
    internal async Task<long> AllocateAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        // A separate committed allocation permits gaps without holding a counter lock through order persistence.
        await using NpgsqlCommand command = dataSource.CreateCommand("""
            INSERT INTO sales.order_numbers (organization_id, last_number)
            VALUES (@organization_id, 1)
            ON CONFLICT (organization_id) DO UPDATE
            SET last_number = sales.order_numbers.last_number + 1
            RETURNING last_number
            """);
        command.Parameters.AddWithValue("organization_id", organizationId);
        return (long)(await command.ExecuteScalarAsync(cancellationToken))!;
    }
}
