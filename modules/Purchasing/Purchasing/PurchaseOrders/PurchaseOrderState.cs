namespace ModulithFoundry.Modules.Purchasing.PurchaseOrders;

internal sealed record PurchaseOrderState(
    string Code,
    string SupplierReference,
    string Currency,
    PurchaseOrderStatus Status,
    IReadOnlyList<PurchaseOrderLineState> Lines
);

internal sealed record PurchaseOrderLineState(string ItemCode, decimal Quantity, decimal UnitPrice);

internal enum PurchaseOrderStatus
{
    Draft = 1,
    Issued = 2,
}
