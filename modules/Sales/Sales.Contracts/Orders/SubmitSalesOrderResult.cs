namespace ModulithFoundry.Modules.Sales.Contracts;

public abstract record SubmitSalesOrderResult
{
    private SubmitSalesOrderResult() { }

    public sealed record Submitted(SalesOrderView Order) : SubmitSalesOrderResult;

    public sealed record InvalidExpectedVersion : SubmitSalesOrderResult;

    public sealed record NotFound : SubmitSalesOrderResult;

    public sealed record NotDraft : SubmitSalesOrderResult;

    public sealed record VersionConflict : SubmitSalesOrderResult;

    public sealed record PermissionDenied : SubmitSalesOrderResult;
}
