namespace ModulithFoundry.Modules.Inventory.Contracts;

public abstract record CorrectStockQuantityResult
{
    private CorrectStockQuantityResult() { }

    public sealed record Corrected(StockPositionView Position) : CorrectStockQuantityResult;

    public sealed record Unchanged(StockPositionView Position) : CorrectStockQuantityResult;

    public sealed record Invalid(string Field, string Detail) : CorrectStockQuantityResult;

    public sealed record NotFound : CorrectStockQuantityResult;

    public sealed record VersionConflict : CorrectStockQuantityResult;

    public sealed record PermissionDenied : CorrectStockQuantityResult;
}
