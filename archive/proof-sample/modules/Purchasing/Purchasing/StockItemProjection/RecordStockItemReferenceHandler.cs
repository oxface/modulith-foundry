using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ModulithFoundry.Modules.Inventory.Contracts;
using ModulithFoundry.Modules.Purchasing.Persistence;
using ModulithFoundry.Modules.Purchasing.StockItemProjection.Persistence;

namespace ModulithFoundry.Modules.Purchasing.StockItemProjection;

internal sealed class RecordStockItemReferenceHandler(
    PurchasingDbContext context,
    TimeProvider timeProvider
)
{
    internal async Task HandleAsync(
        StockItemReferenceChangedV1 message,
        CancellationToken cancellationToken
    )
    {
        StockItemReferenceStateV1 item = message.Item;
        if (
            message.MessageId == Guid.Empty
            || item.OrganizationId == Guid.Empty
            || item.StockItemId == Guid.Empty
            || item.Revision <= 0
            || string.IsNullOrWhiteSpace(item.Sku)
            || item.Sku.Length > 64
            || string.IsNullOrWhiteSpace(item.Description)
            || item.Description.Length > 200
            || string.IsNullOrWhiteSpace(item.BaseUnitCode)
            || item.BaseUnitCode.Length > 16
        )
            throw new InvalidDataException("Stock Item reference event is invalid.");
        string fingerprint = Convert.ToHexString(
            SHA256.HashData(
                JsonSerializer.SerializeToUtf8Bytes(
                    new
                    {
                        message.MessageId,
                        item.OrganizationId,
                        item.StockItemId,
                        item.Sku,
                        item.Description,
                        item.BaseUnitCode,
                        item.IsActive,
                        item.Revision,
                        CreatedAt = message.CreatedAt.ToUniversalTime(),
                    }
                )
            )
        );
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
        StockItemReferenceReceipt? receipt = await context
            .StockItemReferenceInbox.IgnoreQueryFilters([
                PurchasingDbContext.OrganizationScopeFilter,
            ])
            .SingleOrDefaultAsync(x => x.MessageId == message.MessageId, cancellationToken);
        if (receipt is not null)
        {
            if (receipt.Fingerprint != fingerprint)
                throw new InvalidDataException(
                    "Stock Item reference MessageId was reused with different content."
                );
            return;
        }
        if (!checkpoint.IsReady || item.Revision > checkpoint.SnapshotWatermark)
        {
            StockItemReferenceProjection? row = await context
                .StockItemReferences.IgnoreQueryFilters([
                    PurchasingDbContext.OrganizationScopeFilter,
                ])
                .SingleOrDefaultAsync(
                    x =>
                        x.OrganizationId == item.OrganizationId
                        && x.StockItemId == item.StockItemId,
                    cancellationToken
                );
            if (row is null)
                context.StockItemReferences.Add(StockItemReferenceProjection.From(item));
            else
                row.Apply(item);
        }
        context.StockItemReferenceInbox.Add(
            StockItemReferenceReceipt.Processed(
                message.MessageId,
                item.OrganizationId,
                fingerprint,
                timeProvider.GetUtcNow()
            )
        );
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
