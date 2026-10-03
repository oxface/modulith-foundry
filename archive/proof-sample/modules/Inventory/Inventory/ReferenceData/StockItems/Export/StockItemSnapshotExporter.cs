using System.Data;
using Microsoft.EntityFrameworkCore;
using ModulithFoundry.Modules.Inventory.Contracts;
using ModulithFoundry.Modules.Inventory.Persistence;

namespace ModulithFoundry.Modules.Inventory.ReferenceData.StockItems.Export;

internal sealed class StockItemSnapshotExporter(InventoryDbContext context)
    : IStockItemSnapshotExporter
{
    public async Task<StockItemSnapshotV1> ExportAsync(
        CancellationToken cancellationToken = default
    )
    {
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.RepeatableRead,
            cancellationToken
        );
        await context.Database.ExecuteSqlRawAsync("SET TRANSACTION READ ONLY", cancellationToken);
        long watermark = await context
            .StockItemReferenceFeeds.Select(feed => feed.Revision)
            .SingleAsync(cancellationToken);
        List<StockItemReferenceStateV1> items = await context
            .StockItems.IgnoreQueryFilters([InventoryDbContext.OrganizationScopeFilter])
            .AsNoTracking()
            .OrderBy(item => item.Id)
            .Select(item => new StockItemReferenceStateV1(
                item.OrganizationId,
                item.Id,
                item.Sku,
                item.Description,
                item.BaseUnitCode,
                item.IsActive,
                EF.Property<long>(item, StockItemReferencePublisher.RevisionProperty)
            ))
            .Take(10_001)
            .ToListAsync(cancellationToken);
        if (items.Count > 10_000)
            throw new InvalidOperationException(
                "Stock Item bootstrap export exceeds the supported 10,000-item catalog."
            );
        await transaction.CommitAsync(cancellationToken);
        return new(watermark, items);
    }
}
