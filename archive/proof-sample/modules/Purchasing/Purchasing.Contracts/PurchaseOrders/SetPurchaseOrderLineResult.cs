namespace ModulithFoundry.Modules.Purchasing.Contracts;

public abstract record SetPurchaseOrderLineResult
{
    private SetPurchaseOrderLineResult() { }

    public sealed record Changed(PurchaseOrderView Order) : SetPurchaseOrderLineResult;

    public sealed record Unchanged(PurchaseOrderView Order) : SetPurchaseOrderLineResult;

    public sealed record NotFound : SetPurchaseOrderLineResult;

    public sealed record PermissionDenied : SetPurchaseOrderLineResult;

    public sealed record VersionConflict : SetPurchaseOrderLineResult;

    public sealed record Rejected(string Reason) : SetPurchaseOrderLineResult;

    public sealed record Invalid(string Field, string Message) : SetPurchaseOrderLineResult;
}
