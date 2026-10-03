namespace ModulithFoundry.Modules.Purchasing.Contracts;

public abstract record IssuePurchaseOrderResult
{
    private IssuePurchaseOrderResult() { }

    public sealed record Issued(PurchaseOrderView Order) : IssuePurchaseOrderResult;

    public sealed record NotFound : IssuePurchaseOrderResult;

    public sealed record PermissionDenied : IssuePurchaseOrderResult;

    public sealed record VersionConflict : IssuePurchaseOrderResult;

    public sealed record Rejected(string Reason) : IssuePurchaseOrderResult;

    public sealed record Invalid(string Field, string Message) : IssuePurchaseOrderResult;
}
