namespace ModulithFoundry.Modules.Sales.Orders;

internal sealed record SalesOrderLineInput(Guid StockItemId, string Sku, string Description, string BaseUnitCode, decimal Quantity, decimal UnitPrice);
