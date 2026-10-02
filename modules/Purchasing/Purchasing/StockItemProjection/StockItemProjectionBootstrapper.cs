using Microsoft.EntityFrameworkCore;
using ModulithFoundry.Modules.Inventory.Contracts;
using ModulithFoundry.Modules.Purchasing.Contracts;
using ModulithFoundry.Modules.Purchasing.Persistence;
using ModulithFoundry.Modules.Purchasing.StockItemProjection.Persistence;

namespace ModulithFoundry.Modules.Purchasing.StockItemProjection;

internal sealed class StockItemProjectionBootstrapper(
    PurchasingDbContext context,
    IStockItemSnapshotExporter exporter,
    StockItemSubscriptionBarrier subscription
) : IStockItemProjectionBootstrapper
{
    public async Task EnsureInitializedAsync(CancellationToken cancellationToken = default)
    {
        await subscription.WaitAsync(cancellationToken);
        if (
            await context
                .StockItemBootstrapCheckpoints.AsNoTracking()
                .AnyAsync(x => x.IsReady, cancellationToken)
        )
            return;
        StockItemSnapshotV1 snapshot = await exporter.ExportAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            cancellationToken
        );
        StockItemBootstrapCheckpoint checkpoint = (
            await context
                .StockItemBootstrapCheckpoints.FromSqlRaw(
                    "SELECT * FROM purchasing.stock_item_bootstrap WHERE id = 1 FOR UPDATE"
                )
                .ToListAsync(cancellationToken)
        ).Single();
        if (checkpoint.IsReady)
            return;
        List<StockItemReferenceProjection> rows = await context
            .StockItemReferences.IgnoreQueryFilters([PurchasingDbContext.OrganizationScopeFilter])
            .ToListAsync(cancellationToken);
        var existing = rows.ToDictionary(x => (x.OrganizationId, x.StockItemId));
        foreach (StockItemReferenceStateV1 item in snapshot.Items)
        {
            if (existing.TryGetValue((item.OrganizationId, item.StockItemId), out var row))
                row.Apply(item);
            else
                context.StockItemReferences.Add(StockItemReferenceProjection.From(item));
        }
        checkpoint.Complete(snapshot.HighWatermark);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
