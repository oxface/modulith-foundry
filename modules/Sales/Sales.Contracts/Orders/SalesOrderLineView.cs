using ModulithFoundry.Modules.Inventory.Contracts;

namespace ModulithFoundry.Modules.Sales.Contracts;

public sealed record SalesOrderLineView(
    int LineNumber, StockItemId StockItemId, string Sku, string Description, string BaseUnitCode,
    decimal Quantity, decimal UnitPrice, decimal LineAmount);
