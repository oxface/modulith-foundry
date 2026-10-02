namespace ModulithFoundry.Modules.Purchasing.Contracts;

public sealed record PurchaseOrderSummaryView(
    Guid PurchaseOrderId,
    string Code,
    string Currency,
    bool IsIssued,
    int LineCount,
    decimal Total,
    long Version
);
