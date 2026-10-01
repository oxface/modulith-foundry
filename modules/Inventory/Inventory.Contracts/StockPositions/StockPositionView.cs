namespace ModulithFoundry.Modules.Inventory.Contracts;

public sealed record StockPositionView(
    StockPositionId StockPositionId,
    StockItemId StockItemId,
    StockingLocationId StockingLocationId,
    string Sku,
    string StockingLocationCode,
    string BaseUnitCode,
    decimal OnHandQuantity,
    decimal ReservedQuantity,
    decimal AvailableQuantity,
    long Version
);
