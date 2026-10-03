namespace ModulithFoundry.Modules.Sales.Contracts;

public abstract record GetSalesOrderResult
{
    private GetSalesOrderResult() { }

    public sealed record Found(SalesOrderView Order) : GetSalesOrderResult;

    public sealed record NotFound : GetSalesOrderResult;

    public sealed record PermissionDenied : GetSalesOrderResult;
}
