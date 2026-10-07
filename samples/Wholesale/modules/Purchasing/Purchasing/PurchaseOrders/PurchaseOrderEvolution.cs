using ModulithFoundry.Samples.Wholesale.Purchasing.Contracts;

namespace ModulithFoundry.Samples.Wholesale.Purchasing.PurchaseOrders;

internal static class PurchaseOrderEvolution
{
    internal static PurchaseOrderHistory Rehydrate(
        Guid id,
        long version,
        DateTimeOffset recordedAt,
        IEnumerable<IPurchaseOrderEvent> events
    ) => Evolve(null, events).ToHistory(id, version, recordedAt);

    internal static PurchaseOrderState Evolve(
        PurchaseOrderState? state,
        IEnumerable<IPurchaseOrderEvent> events
    )
    {
        PurchaseOrderDrafted? draft = state is null
            ? null
            : new PurchaseOrderDrafted(state.Code, state.SupplierReference, state.Currency);
        var lines =
            state?.Lines.ToDictionary(line => line.ItemCode, StringComparer.Ordinal)
            ?? new Dictionary<string, PurchaseOrderLine>(StringComparer.Ordinal);
        foreach (IPurchaseOrderEvent @event in events)
        {
            switch (@event)
            {
                case PurchaseOrderDrafted opened
                    when draft is null
                        && !string.IsNullOrWhiteSpace(opened.Code)
                        && !string.IsNullOrWhiteSpace(opened.SupplierReference)
                        && !string.IsNullOrWhiteSpace(opened.Currency):
                    draft = opened;
                    break;
                case PurchaseOrderLineSet line
                    when draft is not null
                        && !string.IsNullOrWhiteSpace(line.ItemCode)
                        && line.Quantity > 0
                        && line.UnitPrice >= 0:
                    lines[line.ItemCode] = new PurchaseOrderLine(
                        line.ItemCode,
                        line.Quantity,
                        line.UnitPrice
                    );
                    break;
                default:
                    throw new InvalidOperationException(
                        "Invalid purchase-order event sequence or business values."
                    );
            }
        }
        if (draft is null)
            throw new InvalidOperationException("The purchase order has not been drafted.");
        return new PurchaseOrderState(
            draft.Code,
            draft.SupplierReference,
            draft.Currency,
            Array.AsReadOnly(lines.Values.ToArray())
        );
    }
}
