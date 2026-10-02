namespace ModulithFoundry.Modules.Purchasing.Contracts;

public sealed record PurchaseOrderView(
    Guid PurchaseOrderId,
    string Code,
    string SupplierReference,
    string Currency,
    string Status,
    long Version,
    IReadOnlyList<PurchaseOrderLineView> Lines,
    DateTimeOffset RecordedAt
);
