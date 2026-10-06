namespace ModulithFoundry.Samples.Wholesale.Purchasing.Contracts;

public interface IPurchaseOrderHistory
{
    Task<PurchaseOrderHistory?> ReadCurrentAsync(Guid id, CancellationToken cancellationToken);
    Task<PurchaseOrderHistory?> ReadAtVersionAsync(
        Guid id,
        long version,
        CancellationToken cancellationToken
    );
    Task<PurchaseOrderHistory?> ReadAsOfAsync(
        Guid id,
        DateTimeOffset recordedAt,
        CancellationToken cancellationToken
    );
}
