namespace ModulithFoundry.Modules.Inventory.Contracts;

public abstract record StockPositionRebuildResult
{
    private StockPositionRebuildResult() { }
    public sealed record Rebuilt(StockPositionId StockPositionId, long Version, bool PreviousModelMatched) : StockPositionRebuildResult;

    public sealed record NotFound : StockPositionRebuildResult;
    public sealed record PermissionDenied : StockPositionRebuildResult;
}
