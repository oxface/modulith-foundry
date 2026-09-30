namespace ModulithFoundry.Modules.Inventory.Contracts;

public sealed record StockingLocationView(
    StockingLocationId StockingLocationId,
    string Code,
    string Name,
    bool IsActive);
