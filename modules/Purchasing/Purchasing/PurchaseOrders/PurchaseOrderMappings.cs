using ModulithFoundry.Modules.Purchasing.Contracts;

namespace ModulithFoundry.Modules.Purchasing.PurchaseOrders;

internal static class PurchaseOrderMappings
{
    internal static PurchaseOrderView ToView(
        this PurchaseOrderState state,
        Guid id,
        long version,
        DateTimeOffset recordedAt
    ) =>
        new(
            id,
            state.Code,
            state.SupplierReference,
            state.Currency,
            state.Status switch
            {
                PurchaseOrderStatus.Draft => "draft",
                PurchaseOrderStatus.Issued => "issued",
                _ => throw new InvalidOperationException("Unknown Purchase Order status."),
            },
            version,
            state
                .Lines.Select(x => new PurchaseOrderLineView(x.ItemCode, x.Quantity, x.UnitPrice))
                .ToArray(),
            recordedAt
        );
}
