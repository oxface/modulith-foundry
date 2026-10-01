using ModulithFoundry.Modules.Inventory.Contracts;
using ModulithFoundry.Modules.Sales.Contracts;

namespace ModulithFoundry.Modules.Sales.Orders;

internal static class SalesOrderMappings
{
    internal static SalesOrderView ToView(this SalesOrder order) =>
        new(
            new SalesOrderId(order.Id),
            order.OrderNumber,
            new CustomerId(order.CustomerId),
            order.Currency,
            order.TotalAmount,
            [
                .. order
                    .Lines.OrderBy(line => line.LineNumber)
                    .Select(line => new SalesOrderLineView(
                        line.LineNumber,
                        new StockItemId(line.StockItemId),
                        line.Sku,
                        line.Description,
                        line.BaseUnitCode,
                        line.Quantity,
                        line.UnitPrice,
                        line.LineAmount
                    )),
            ]
        );
}
