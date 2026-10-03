using ModulithFoundry.Modules.Sales.Contracts;

namespace ModulithFoundry.Api.Modules.Sales.Orders;

internal static class SalesOrderResponses
{
    internal static Order ToResponse(SalesOrderView order) =>
        new(
            order.SalesOrderId.Value,
            order.OrderNumber,
            order.CustomerId.Value,
            order.Currency,
            order.TotalAmount,
            [
                .. order.Lines.Select(line => new Line(
                    line.LineNumber,
                    line.StockItemId.Value,
                    line.Sku,
                    line.Description,
                    line.BaseUnitCode,
                    line.Quantity,
                    line.UnitPrice,
                    line.LineAmount
                )),
            ],
            SalesOrderStatusValues.ToValue(order.Status),
            order.Version,
            order.SubmittedBy?.Value,
            order.SubmittedAt,
            order.ApprovedBy?.Value,
            order.ApprovedAt,
            order.CancelledBy?.Value,
            order.CancelledAt,
            order.CancellationReason
        );

    internal sealed record Order(
        Guid SalesOrderId,
        long OrderNumber,
        Guid CustomerId,
        string Currency,
        decimal TotalAmount,
        IReadOnlyList<Line> Lines,
        string Status,
        long Version,
        Guid? SubmittedBy,
        DateTimeOffset? SubmittedAt,
        Guid? ApprovedBy,
        DateTimeOffset? ApprovedAt,
        Guid? CancelledBy,
        DateTimeOffset? CancelledAt,
        string? CancellationReason
    );

    internal sealed record Line(
        int LineNumber,
        Guid StockItemId,
        string Sku,
        string Description,
        string BaseUnitCode,
        decimal Quantity,
        decimal UnitPrice,
        decimal LineAmount
    );
}
