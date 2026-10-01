using ModulithFoundry.Modules.Inventory.Contracts;

namespace ModulithFoundry.Modules.Sales.Contracts;

public sealed record DraftSalesOrderLine(StockItemId StockItemId, decimal Quantity, decimal UnitPrice);
