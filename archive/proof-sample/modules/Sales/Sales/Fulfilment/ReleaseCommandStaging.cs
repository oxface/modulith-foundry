using ModulithFoundry.Modules.Inventory.Contracts;
using ModulithFoundry.Modules.Sales.Audit;
using ModulithFoundry.Modules.Sales.Messaging.Persistence;
using ModulithFoundry.Modules.Sales.Persistence;

namespace ModulithFoundry.Modules.Sales.Fulfilment;

internal static class ReleaseCommandStaging
{
    internal static void Stage(
        SalesDbContext context,
        OrderFulfilmentProcess process,
        OrderFulfilmentLine line,
        DateTimeOffset now
    )
    {
        if (!process.QueueRelease(line, now))
            return;
        context.OutboxMessages.Add(
            SalesOutboxMessage.Stage(
                new ReleaseReservationV1(
                    line.ReleaseCommandMessageId!.Value,
                    process.OrganizationId,
                    line.ReleaseOperationId!.Value,
                    process.Id,
                    process.OrderNumber,
                    line.LineNumber,
                    line.OperationId,
                    line.ReservationId!.Value,
                    line.StockItemId,
                    process.StockingLocationId!.Value,
                    now
                )
            )
        );
        context.AuditEntries.Add(
            SalesAuditEntry.WorkflowDecision(
                process.OrganizationId,
                SalesAuditActions.ReservationReleaseQueued,
                process.Id,
                SalesAuditOutcomes.Succeeded,
                null,
                new
                {
                    line.LineNumber,
                    line.ReleaseOperationId,
                    line.ReservationId,
                },
                now
            )
        );
    }
}
