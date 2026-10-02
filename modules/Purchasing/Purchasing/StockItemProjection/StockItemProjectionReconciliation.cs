using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ModulithFoundry.Modules.Inventory.Contracts;
using ModulithFoundry.Modules.Purchasing.Contracts;
using ModulithFoundry.Modules.Purchasing.Persistence;
using ModulithFoundry.Modules.Purchasing.StockItemProjection.Persistence;

namespace ModulithFoundry.Modules.Purchasing.StockItemProjection;

internal sealed partial class StockItemProjectionReconciliation(
    PurchasingDbContext context,
    IStockItemSnapshotExporter exporter,
    StockItemSubscriptionBarrier subscription,
    ILogger<StockItemProjectionReconciliation> logger
) : IStockItemProjectionReconciliation
{
    public Task<StockItemProjectionComparison> InspectAsync(
        CancellationToken cancellationToken = default
    ) => CompareAsync(repair: false, cancellationToken);

    public Task<StockItemProjectionComparison> RepairAsync(
        CancellationToken cancellationToken = default
    ) => CompareAsync(repair: true, cancellationToken);

    private async Task<StockItemProjectionComparison> CompareAsync(
        bool repair,
        CancellationToken cancellationToken
    )
    {
        await subscription.WaitAsync(cancellationToken);
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
        await context.Entry(checkpoint).ReloadAsync(cancellationToken);
        if (!checkpoint.IsReady)
            throw new InvalidOperationException(
                "Initialize the Stock Item reference projection before reconciliation."
            );
        if (snapshot.HighWatermark < checkpoint.SnapshotWatermark)
            throw new InvalidDataException(
                "Inventory snapshot is older than the installed boundary; retry with a fresh snapshot. Persistent regression requires coordinated source/consumer recovery."
            );

        List<StockItemReferenceProjection> rows = await context
            .StockItemReferences.IgnoreQueryFilters([PurchasingDbContext.OrganizationScopeFilter])
            .ToListAsync(cancellationToken);
        // Reused administrative scopes must not compare or repair stale tracked state.
        foreach (StockItemReferenceProjection row in rows)
            await context.Entry(row).ReloadAsync(cancellationToken);
        var existing = rows.ToDictionary(x => (x.OrganizationId, x.StockItemId));
        var keys = snapshot.Items.Select(x => (x.OrganizationId, x.StockItemId)).ToHashSet();
        int missing = 0,
            different = 0;
        int newer = rows.Count(x => x.SourceRevision > snapshot.HighWatermark);
        foreach (StockItemReferenceStateV1 item in snapshot.Items)
        {
            if (!existing.TryGetValue((item.OrganizationId, item.StockItemId), out var row))
            {
                missing++;
                if (repair)
                    context.StockItemReferences.Add(StockItemReferenceProjection.From(item));
            }
            else if (row.SourceRevision <= snapshot.HighWatermark && !row.Matches(item))
            {
                different++;
                if (repair)
                    row.Restore(item);
            }
        }
        StockItemReferenceProjection[] unexpected = rows.Where(x =>
                x.SourceRevision <= snapshot.HighWatermark
                && !keys.Contains((x.OrganizationId, x.StockItemId))
            )
            .ToArray();
        var comparison = new StockItemProjectionComparison(
            snapshot.HighWatermark,
            missing,
            different,
            unexpected.Length,
            newer
        );
        if (repair)
        {
            context.StockItemReferences.RemoveRange(unexpected);
            checkpoint.Complete(snapshot.HighWatermark);
            await context.SaveChangesAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        if (repair)
            Repaired(logger, snapshot.HighWatermark, missing, different, unexpected.Length, newer);
        return comparison;
    }

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Warning,
        Message = "Purchasing reference repair committed at snapshot {Watermark}: missing {Missing}, different {Different}, unexpected {Unexpected}, preserved newer {Newer}."
    )]
    private static partial void Repaired(
        ILogger logger,
        long watermark,
        int missing,
        int different,
        int unexpected,
        int newer
    );
}
