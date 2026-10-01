namespace ModulithFoundry.Modules.Inventory.Contracts;

public abstract record CreateStockingLocationResult
{
    private CreateStockingLocationResult() { }

    public sealed record Created(StockingLocationView Location) : CreateStockingLocationResult;

    public sealed record Invalid(string Field, string Detail) : CreateStockingLocationResult;

    public sealed record CodeUnavailable(string Code) : CreateStockingLocationResult;

    public sealed record PermissionDenied : CreateStockingLocationResult;
}
