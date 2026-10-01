namespace ModulithFoundry.Modules.Sales.Contracts;

public abstract record GetSalesOrderActivityResult
{
    private GetSalesOrderActivityResult() { }

    public sealed record Found(IReadOnlyList<SalesOrderActivityEntry> Entries)
        : GetSalesOrderActivityResult;

    public sealed record NotFound : GetSalesOrderActivityResult;

    public sealed record PermissionDenied : GetSalesOrderActivityResult;
}
