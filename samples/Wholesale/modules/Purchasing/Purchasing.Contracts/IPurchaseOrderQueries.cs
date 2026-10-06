namespace ModulithFoundry.Samples.Wholesale.Purchasing.Contracts;

public interface IPurchaseOrderQueries
{
    Task<PurchaseOrderHistory?> ReadCurrentAsync(Guid id, CancellationToken cancellationToken);
    Task<PurchaseOrderSummary?> ReadSummaryAsync(Guid id, CancellationToken cancellationToken);
}

public sealed record PurchaseOrderSummary(
    Guid Id,
    long Version,
    DateTimeOffset RecordedAt,
    string Code,
    string Currency,
    int LineCount,
    decimal Total
);
