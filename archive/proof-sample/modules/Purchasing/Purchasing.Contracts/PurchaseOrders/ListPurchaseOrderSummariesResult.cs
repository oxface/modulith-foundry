namespace ModulithFoundry.Modules.Purchasing.Contracts;

public abstract record ListPurchaseOrderSummariesResult
{
    private ListPurchaseOrderSummariesResult() { }

    public sealed record Listed(IReadOnlyList<PurchaseOrderSummaryView> Orders)
        : ListPurchaseOrderSummariesResult;

    public sealed record PermissionDenied : ListPurchaseOrderSummariesResult;

    public sealed record Invalid(string Message) : ListPurchaseOrderSummariesResult;
}
