namespace ModulithFoundry.Modules.Sales.Contracts;

public abstract record CancelSalesOrderResult
{
    private CancelSalesOrderResult() { }

    public sealed record Cancelled(SalesOrderView Order) : CancelSalesOrderResult;

    public sealed record Unchanged(SalesOrderView Order) : CancelSalesOrderResult;

    public sealed record InvalidExpectedVersion : CancelSalesOrderResult;

    public sealed record InvalidReason : CancelSalesOrderResult;

    public sealed record NotFound : CancelSalesOrderResult;

    public sealed record VersionConflict : CancelSalesOrderResult;

    public sealed record PermissionDenied : CancelSalesOrderResult;
}
