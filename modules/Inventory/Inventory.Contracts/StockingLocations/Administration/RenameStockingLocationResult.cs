namespace ModulithFoundry.Modules.Inventory.Contracts;

public abstract record RenameStockingLocationResult
{
    private RenameStockingLocationResult() { }

    public sealed record Renamed(StockingLocationView Location) : RenameStockingLocationResult;

    public sealed record Unchanged(StockingLocationView Location) : RenameStockingLocationResult;

    public sealed record Invalid(string Field, string Detail) : RenameStockingLocationResult;

    public sealed record NotFound : RenameStockingLocationResult;

    public sealed record PermissionDenied : RenameStockingLocationResult;
}
