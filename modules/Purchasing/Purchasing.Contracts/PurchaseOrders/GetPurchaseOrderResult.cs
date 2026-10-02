namespace ModulithFoundry.Modules.Purchasing.Contracts;

public abstract record GetPurchaseOrderResult
{
    private GetPurchaseOrderResult() { }

    public sealed record Found(PurchaseOrderView Order) : GetPurchaseOrderResult;

    public sealed record NotFound : GetPurchaseOrderResult;

    public sealed record PermissionDenied : GetPurchaseOrderResult;

    public sealed record Invalid(string Message) : GetPurchaseOrderResult;
}
