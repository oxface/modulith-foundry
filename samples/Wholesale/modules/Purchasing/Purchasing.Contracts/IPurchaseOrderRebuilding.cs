namespace ModulithFoundry.Samples.Wholesale.Purchasing.Contracts;

public interface IPurchaseOrderRebuilding
{
    Task<PurchaseOrderRebuildResult> RebuildAsync(
        Guid id,
        CancellationToken cancellationToken = default
    );
}

public abstract record PurchaseOrderRebuildResult
{
    private PurchaseOrderRebuildResult() { }

    public sealed record NotFound : PurchaseOrderRebuildResult;

    public sealed record Changed(long Version, DateTimeOffset RecordedAt)
        : PurchaseOrderRebuildResult;
}
