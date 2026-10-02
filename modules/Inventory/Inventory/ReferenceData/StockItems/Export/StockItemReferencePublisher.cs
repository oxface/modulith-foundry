using Microsoft.EntityFrameworkCore;
using ModulithFoundry.Modules.Inventory.Contracts;
using ModulithFoundry.Modules.Inventory.Messaging.Persistence;
using ModulithFoundry.Modules.Inventory.Persistence;

namespace ModulithFoundry.Modules.Inventory.ReferenceData.StockItems.Export;

internal sealed class StockItemReferencePublisher(InventoryDbContext context)
{
    internal const string RevisionProperty = "ReferenceRevision";
    private StockItemReferenceFeed? feed;

    internal async Task LockAsync(CancellationToken cancellationToken)
    {
        if (context.Database.CurrentTransaction is null)
            throw new InvalidOperationException(
                "Reference publication requires the writer's explicit transaction."
            );
        feed = (
            await context
                .StockItemReferenceFeeds.FromSqlRaw(
                    "SELECT * FROM inventory.stock_item_reference_feed WHERE id = 1 FOR UPDATE"
                )
                .ToListAsync(cancellationToken)
        ).Single();
        // Tracking queries preserve earlier values in a reused context. Refresh only the
        // locked technical row; never clear unrelated staged work from the context.
        await context.Entry(feed).ReloadAsync(cancellationToken);
    }

    internal void Stage(StockItem item, DateTimeOffset now)
    {
        if (feed is null || context.Database.CurrentTransaction is null)
            throw new InvalidOperationException(
                "Lock the reference feed before reading or changing a Stock Item."
            );
        long revision = feed.Advance();
        context.Entry(item).Property<long>(RevisionProperty).CurrentValue = revision;
        context.OutboxMessages.Add(
            InventoryOutboxMessage.Stage(
                new StockItemReferenceChangedV1(
                    Guid.CreateVersion7(now),
                    new(
                        item.OrganizationId,
                        item.Id,
                        item.Sku,
                        item.Description,
                        item.BaseUnitCode,
                        item.IsActive,
                        revision
                    ),
                    now
                )
            )
        );
    }
}
