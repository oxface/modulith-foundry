namespace ModulithFoundry.Modules.Inventory.Contracts;

public sealed record StockItemView(
    StockItemId StockItemId,
    string Sku,
    string Description,
    string BaseUnitCode,
    bool IsActive);
