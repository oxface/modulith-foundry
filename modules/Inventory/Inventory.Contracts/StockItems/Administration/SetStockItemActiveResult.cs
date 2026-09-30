namespace ModulithFoundry.Modules.Inventory.Contracts;

public abstract record SetStockItemActiveResult
{
    private SetStockItemActiveResult()
    {
    }

    public sealed record Changed(StockItemView Item) : SetStockItemActiveResult;

    public sealed record Unchanged(StockItemView Item) : SetStockItemActiveResult;

    public sealed record Invalid(string Field, string Detail) : SetStockItemActiveResult;

    public sealed record NotFound : SetStockItemActiveResult;

    public sealed record PermissionDenied : SetStockItemActiveResult;
}
