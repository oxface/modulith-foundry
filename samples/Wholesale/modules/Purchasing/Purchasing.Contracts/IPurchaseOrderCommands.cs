namespace ModulithFoundry.Samples.Wholesale.Purchasing.Contracts;

// Staging changes the caller's native context only. Staged is not committed success.
public interface IPurchaseOrderCommands
{
    Task<PurchaseOrderChangeResult> StageDraftAsync(
        DraftPurchaseOrder request,
        CancellationToken cancellationToken
    );
    Task<PurchaseOrderChangeResult> StageLinesAsync(
        ChangePurchaseOrderLines request,
        CancellationToken cancellationToken
    );
}

public sealed record DraftPurchaseOrder(
    Guid Id,
    string Code,
    string SupplierReference,
    string Currency,
    long ExpectedVersion = 0
);

public sealed record ChangePurchaseOrderLines(
    Guid Id,
    long ExpectedVersion,
    IReadOnlyList<PurchaseOrderLine> Lines
);

public abstract record PurchaseOrderChangeResult
{
    public sealed record Staged(PurchaseOrderHistory Proposed) : PurchaseOrderChangeResult;

    public sealed record NotFound : PurchaseOrderChangeResult;

    public sealed record Conflict : PurchaseOrderChangeResult;
}
