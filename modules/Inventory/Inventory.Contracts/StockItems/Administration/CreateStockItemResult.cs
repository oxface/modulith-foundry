namespace ModulithFoundry.Modules.Inventory.Contracts;

public abstract record CreateStockItemResult
{
    private CreateStockItemResult() { }

    public sealed record Created(StockItemView Item) : CreateStockItemResult;

    public sealed record Invalid(string Field, string Detail) : CreateStockItemResult;

    public sealed record SkuUnavailable(string Sku) : CreateStockItemResult;

    public sealed record PermissionDenied : CreateStockItemResult;
}
