using ModulithFoundry.Samples.Wholesale.Purchasing.Contracts;

namespace ModulithFoundry.Samples.Wholesale.Purchasing.PurchaseOrders;

internal sealed record PurchaseOrderState(
    string Code,
    string SupplierReference,
    string Currency,
    IReadOnlyList<PurchaseOrderLine> Lines
)
{
    internal decimal Total => Lines.Sum(line => line.Quantity * line.UnitPrice);

    internal PurchaseOrderHistory ToHistory(Guid id, long version, DateTimeOffset recordedAt) =>
        new(
            id,
            version,
            recordedAt,
            Code,
            SupplierReference,
            Currency,
            Array.AsReadOnly(Lines.ToArray())
        );
}
