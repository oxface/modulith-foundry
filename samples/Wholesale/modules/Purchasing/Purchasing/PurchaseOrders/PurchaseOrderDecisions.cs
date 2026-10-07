using ModulithFoundry.Samples.Wholesale.Purchasing.Contracts;

namespace ModulithFoundry.Samples.Wholesale.Purchasing.PurchaseOrders;

internal static class PurchaseOrderDecisions
{
    internal static void ValidateDraft(DraftPurchaseOrder request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfEqual(request.Id, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfNotEqual(request.ExpectedVersion, 0);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Code);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SupplierReference);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Currency);
    }

    internal static IPurchaseOrderEvent[] Draft(DraftPurchaseOrder request)
    {
        ValidateDraft(request);
        return
        [
            new PurchaseOrderDrafted(request.Code, request.SupplierReference, request.Currency),
        ];
    }

    internal static PurchaseOrderLine[] LineItems(ChangePurchaseOrderLines request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfEqual(request.Id, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfLessThan(request.ExpectedVersion, 1);
        ArgumentNullException.ThrowIfNull(request.Lines);
        PurchaseOrderLine[] items = request.Lines.ToArray();
        ArgumentOutOfRangeException.ThrowIfLessThan(items.Length, 1);
        foreach (var item in items)
        {
            ArgumentNullException.ThrowIfNull(item);
            ArgumentException.ThrowIfNullOrWhiteSpace(item.ItemCode);
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(item.Quantity, 0);
            ArgumentOutOfRangeException.ThrowIfNegative(item.UnitPrice);
        }
        return items;
    }

    internal static IPurchaseOrderEvent[] Lines(IReadOnlyList<PurchaseOrderLine> items) =>
        items
            .Select(item =>
            {
                ArgumentNullException.ThrowIfNull(item);
                ArgumentException.ThrowIfNullOrWhiteSpace(item.ItemCode);
                ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(item.Quantity, 0);
                ArgumentOutOfRangeException.ThrowIfNegative(item.UnitPrice);
                return (IPurchaseOrderEvent)
                    new PurchaseOrderLineSet(item.ItemCode, item.Quantity, item.UnitPrice);
            })
            .ToArray();
}
