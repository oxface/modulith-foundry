namespace ModulithFoundry.Modules.Inventory.Contracts;

public sealed record StockItemReference(
    StockItemId StockItemId,
    string Sku,
    string Description,
    string BaseUnitCode);
