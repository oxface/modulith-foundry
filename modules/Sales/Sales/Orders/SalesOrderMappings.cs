using System.Linq.Expressions;
using ModulithFoundry.Modules.Access.Contracts;
using ModulithFoundry.Modules.Inventory.Contracts;
using ModulithFoundry.Modules.Sales.Contracts;

namespace ModulithFoundry.Modules.Sales.Orders;

internal static class SalesOrderMappings
{
    internal static readonly Expression<Func<SalesOrder, SalesOrderView>> ViewProjection =
        order => new SalesOrderView(
            new SalesOrderId(order.Id),
            order.OrderNumber,
            new CustomerId(order.CustomerId),
            order.Currency,
            order.TotalAmount,
            order
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
                ))
                .ToArray(),
            order.Status,
            order.Version,
            order.SubmittedBy.HasValue ? new UserId(order.SubmittedBy.Value) : (UserId?)null,
            order.SubmittedAt,
            order.ApprovedBy.HasValue ? new UserId(order.ApprovedBy.Value) : (UserId?)null,
            order.ApprovedAt
        );

    private static readonly Func<SalesOrder, SalesOrderView> MapView = ViewProjection.Compile();

    internal static SalesOrderView ToView(this SalesOrder order) => MapView(order);
}
