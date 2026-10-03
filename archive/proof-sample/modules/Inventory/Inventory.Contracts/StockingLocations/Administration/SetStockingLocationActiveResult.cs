namespace ModulithFoundry.Modules.Inventory.Contracts;

public abstract record SetStockingLocationActiveResult
{
    private SetStockingLocationActiveResult() { }

    public sealed record Changed(StockingLocationView Location) : SetStockingLocationActiveResult;

    public sealed record Unchanged(StockingLocationView Location) : SetStockingLocationActiveResult;

    public sealed record Invalid(string Field, string Detail) : SetStockingLocationActiveResult;

    public sealed record NotFound : SetStockingLocationActiveResult;

    public sealed record PermissionDenied : SetStockingLocationActiveResult;
}
