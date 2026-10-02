namespace ModulithFoundry.Modules.Purchasing.Replenishment;

internal sealed record ReplenishmentStockItemSnapshot(
    Guid StockItemId,
    string Sku,
    string Description,
    string BaseUnitCode,
    long SourceRevision
);
