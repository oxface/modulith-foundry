using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using ModulithFoundry.Modules.Inventory.Persistence;

namespace ModulithFoundry.Modules.Inventory.StockPositions.Persistence;

/// <summary>Normal writers overlap; reconstruction waits until every earlier writer has committed.</summary>
internal sealed class StockPositionWriteGate(InventoryDbContext context)
{
    internal async Task<IDbContextTransaction> BeginAsync(
        Guid organizationId,
        bool exclusive,
        CancellationToken cancellationToken
    )
    {
        IDbContextTransaction transaction = await context.Database.BeginTransactionAsync(
            cancellationToken
        );
        try
        {
            string key = $"inventory.stock-position:{organizationId:D}";
            if (exclusive)
            {
                await context.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))",
                    cancellationToken
                );
            }
            else
            {
                await context.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT pg_advisory_xact_lock_shared(hashtextextended({key}, 0))",
                    cancellationToken
                );
            }
            return transaction;
        }
        catch
        {
            await transaction.DisposeAsync();
            throw;
        }
    }
}
