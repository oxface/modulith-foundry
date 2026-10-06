namespace ModulithFoundry.Samples.Wholesale.Purchasing.Contracts;

public sealed record PurchaseOrderHistory(
    Guid Id,
    long Version,
    DateTimeOffset RecordedAt,
    string Code,
    string SupplierReference,
    string Currency,
    IReadOnlyList<PurchaseOrderLine> Lines
)
{
    public decimal Total => Lines.Sum(line => line.Quantity * line.UnitPrice);
}

public sealed record PurchaseOrderLine(string ItemCode, decimal Quantity, decimal UnitPrice);
