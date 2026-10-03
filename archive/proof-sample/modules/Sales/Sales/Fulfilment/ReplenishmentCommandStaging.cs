using ModulithFoundry.Modules.Purchasing.Contracts;
using ModulithFoundry.Modules.Sales.Audit;
using ModulithFoundry.Modules.Sales.Messaging.Persistence;
using ModulithFoundry.Modules.Sales.Persistence;

namespace ModulithFoundry.Modules.Sales.Fulfilment;

internal static class ReplenishmentCommandStaging
{
    internal static void Stage(
        SalesDbContext context,
        OrderFulfilmentProcess process,
        OrderFulfilmentLine line,
        DateTimeOffset now
    )
    {
        if (!process.QueueReplenishment(line, now))
            return;
        context.OutboxMessages.Add(
            SalesOutboxMessage.Stage(
                new CreateReplenishmentRequirementV1(
                    line.ReplenishmentCommandMessageId!.Value,
                    process.OrganizationId,
                    line.OperationId,
                    process.Id,
                    process.OrderNumber,
                    line.LineNumber,
                    line.StockItemId,
                    line.ReplenishmentQuantity!.Value,
                    line.BaseUnitCode,
                    now
                )
            )
        );
        context.AuditEntries.Add(
            SalesAuditEntry.WorkflowDecision(
                process.OrganizationId,
                SalesAuditActions.ReplenishmentQueued,
                process.Id,
                SalesAuditOutcomes.Succeeded,
                null,
                new
                {
                    line.LineNumber,
                    line.OperationId,
                    line.ReplenishmentQuantity,
                },
                now
            )
        );
    }
}
