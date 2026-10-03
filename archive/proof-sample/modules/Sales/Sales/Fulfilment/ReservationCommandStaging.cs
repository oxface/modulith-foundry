using ModulithFoundry.Modules.Inventory.Contracts;
using ModulithFoundry.Modules.Sales.Audit;
using ModulithFoundry.Modules.Sales.Contracts;
using ModulithFoundry.Modules.Sales.Messaging.Persistence;
using ModulithFoundry.Modules.Sales.Orders;
using ModulithFoundry.Modules.Sales.Orders.Activity;
using ModulithFoundry.Modules.Sales.Persistence;

namespace ModulithFoundry.Modules.Sales.Fulfilment;

internal static class ReservationCommandStaging
{
    internal const string DefaultLocationCode = "MAIN";

    internal static void Stage(
        SalesDbContext context,
        OrderFulfilmentProcess process,
        SalesOrder order,
        Guid locationId,
        DateTimeOffset now
    )
    {
        if (!process.QueueReservations(order, locationId, now))
            return;
        foreach (OrderFulfilmentLine line in process.Lines)
            context.OutboxMessages.Add(
                SalesOutboxMessage.Stage(
                    new ReserveStockV1(
                        line.CommandMessageId,
                        process.OrganizationId,
                        line.OperationId,
                        process.Id,
                        process.OrderNumber,
                        line.LineNumber,
                        line.StockItemId,
                        locationId,
                        line.Quantity,
                        line.BaseUnitCode,
                        now
                    )
                )
            );
        context.AuditEntries.Add(
            SalesAuditEntry.WorkflowDecision(
                process.OrganizationId,
                SalesAuditActions.FulfilmentQueued,
                process.Id,
                SalesAuditOutcomes.Succeeded,
                null,
                new
                {
                    process.OrderNumber,
                    process.Version,
                    LineCount = process.Lines.Count,
                    StockingLocationId = locationId,
                },
                now
            )
        );
        context.OrderActivity.Add(
            SalesOrderActivity.RecordFulfilment(
                process,
                order.Version,
                SalesOrderActivityKind.FulfilmentStarted,
                null,
                now
            )
        );
    }
}
