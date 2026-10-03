namespace ModulithFoundry.Modules.Inventory.Contracts;

public abstract record ListStockItemsResult
{
    private ListStockItemsResult() { }

    public sealed record Listed(IReadOnlyList<StockItemView> Items) : ListStockItemsResult;

    public sealed record PermissionDenied : ListStockItemsResult;
}
