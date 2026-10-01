using ModulithFoundry.Modules.Inventory.Contracts;

namespace ModulithFoundry.Modules.Sales.Contracts;

public abstract record CreateDraftSalesOrderResult
{
    private CreateDraftSalesOrderResult() { }
    public sealed record Created(SalesOrderView Order) : CreateDraftSalesOrderResult;
    public sealed record Invalid(string Field, string Detail) : CreateDraftSalesOrderResult;
    public sealed record CustomerNotFound : CreateDraftSalesOrderResult;
    public sealed record ItemsUnavailable(IReadOnlyList<StockItemId> MissingItemIds, IReadOnlyList<StockItemId> InactiveItemIds) : CreateDraftSalesOrderResult;
    public sealed record PermissionDenied : CreateDraftSalesOrderResult;
}
