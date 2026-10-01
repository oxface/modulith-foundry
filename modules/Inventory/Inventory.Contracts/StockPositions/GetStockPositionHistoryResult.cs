namespace ModulithFoundry.Modules.Inventory.Contracts;

public abstract record GetStockPositionHistoryResult
{
    private GetStockPositionHistoryResult() { }

    public sealed record Found(StockPositionHistoryView History) : GetStockPositionHistoryResult;

    public sealed record Invalid(string Field, string Detail) : GetStockPositionHistoryResult;

    public sealed record NotFound : GetStockPositionHistoryResult;

    public sealed record PermissionDenied : GetStockPositionHistoryResult;
}
