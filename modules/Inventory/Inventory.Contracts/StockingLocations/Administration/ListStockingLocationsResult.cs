namespace ModulithFoundry.Modules.Inventory.Contracts;

public abstract record ListStockingLocationsResult
{
    private ListStockingLocationsResult() { }

    public sealed record Listed(IReadOnlyList<StockingLocationView> Locations)
        : ListStockingLocationsResult;

    public sealed record PermissionDenied : ListStockingLocationsResult;
}
