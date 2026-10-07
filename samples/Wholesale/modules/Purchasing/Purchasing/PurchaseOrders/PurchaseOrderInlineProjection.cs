using ModulithFoundry.EventSourcing.EntityFrameworkCore;

namespace ModulithFoundry.Samples.Wholesale.Purchasing.PurchaseOrders;

internal sealed class PurchaseOrderInlineProjection(PurchasingDbContext database)
{
    internal async Task<(
        PurchaseOrderCurrentRow Current,
        PurchaseOrderSummaryRow Summary
    )> LoadAsync(EventStream stream, CancellationToken cancellationToken) =>
        (
            await new InlineProjectionStorage<EventStream, PurchaseOrderCurrentRow>(
                database
            ).LoadAsync(stream, cancellationToken),
            await new InlineProjectionStorage<EventStream, PurchaseOrderSummaryRow>(
                database
            ).LoadAsync(stream, cancellationToken)
        );
}
