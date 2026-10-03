namespace ModulithFoundry.Modules.Inventory.Contracts;

public abstract record GetStockPositionResult
{
    private GetStockPositionResult() { }

    public sealed record Found(StockPositionView Position) : GetStockPositionResult;

    public sealed record Invalid(string Field, string Detail) : GetStockPositionResult;

    public sealed record NotFound : GetStockPositionResult;

    public sealed record PermissionDenied : GetStockPositionResult;
}
