using ModulithFoundry.Modules.Purchasing.PurchaseOrders.Events;
using ModulithFoundry.Modules.Purchasing.PurchaseOrders.Persistence;

namespace ModulithFoundry.Modules.Purchasing.PurchaseOrders;

internal static class PurchaseOrderEvolution
{
    internal static PurchaseOrderState Evolve(
        PurchaseOrderState? state,
        IPurchaseOrderEvent @event
    ) =>
        @event switch
        {
            PurchaseOrderDrafted drafted when state is null => new(
                drafted.Code,
                drafted.SupplierReference,
                drafted.Currency,
                PurchaseOrderStatus.Draft,
                []
            ),
            PurchaseOrderLineSet line when state is not null => state with
            {
                Lines = state
                    .Lines.Where(x => x.ItemCode != line.ItemCode)
                    .Append(
                        new PurchaseOrderLineState(line.ItemCode, line.Quantity, line.UnitPrice)
                    )
                    .ToArray(),
            },
            PurchaseOrderIssued when state is not null => state with
            {
                Status = PurchaseOrderStatus.Issued,
            },
            _ => throw new PurchaseOrderIntegrityException(
                null,
                PurchaseOrderIntegrityFailure.EventOrder
            ),
        };
}
