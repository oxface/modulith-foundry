namespace ModulithFoundry.Modules.Inventory.Contracts;

public abstract record ChangeStockItemDescriptionResult
{
    private ChangeStockItemDescriptionResult()
    {
    }

    public sealed record Changed(StockItemView Item) : ChangeStockItemDescriptionResult;

    public sealed record Unchanged(StockItemView Item) : ChangeStockItemDescriptionResult;

    public sealed record Invalid(string Field, string Detail) : ChangeStockItemDescriptionResult;

    public sealed record NotFound : ChangeStockItemDescriptionResult;

    public sealed record PermissionDenied : ChangeStockItemDescriptionResult;
}
