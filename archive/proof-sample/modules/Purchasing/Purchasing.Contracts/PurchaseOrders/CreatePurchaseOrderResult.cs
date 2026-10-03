namespace ModulithFoundry.Modules.Purchasing.Contracts;

public abstract record CreatePurchaseOrderResult
{
    private CreatePurchaseOrderResult() { }

    public sealed record Created(PurchaseOrderView Order) : CreatePurchaseOrderResult;

    public sealed record CodeUnavailable : CreatePurchaseOrderResult;

    public sealed record PermissionDenied : CreatePurchaseOrderResult;

    public sealed record Invalid(string Field, string Message) : CreatePurchaseOrderResult;
}
