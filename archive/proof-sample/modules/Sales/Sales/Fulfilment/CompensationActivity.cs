using ModulithFoundry.Modules.Sales.Audit;
using ModulithFoundry.Modules.Sales.Contracts;
using ModulithFoundry.Modules.Sales.Orders.Activity;
using ModulithFoundry.Modules.Sales.Persistence;

namespace ModulithFoundry.Modules.Sales.Fulfilment;

internal static class CompensationActivity
{
    internal static void StageCompletion(
        SalesDbContext context,
        OrderFulfilmentProcess process,
        OrderFulfilmentStatus previousStatus,
        long orderVersion,
        DateTimeOffset now
    )
    {
        if (
            previousStatus == OrderFulfilmentStatus.Compensated
            || process.Status != OrderFulfilmentStatus.Compensated
        )
            return;
        context.OrderActivity.Add(
            SalesOrderActivity.RecordFulfilment(
                process,
                orderVersion,
                SalesOrderActivityKind.CompensationCompleted,
                null,
                now
            )
        );
        context.AuditEntries.Add(
            SalesAuditEntry.WorkflowDecision(
                process.OrganizationId,
                SalesAuditActions.CompensationCompleted,
                process.Id,
                SalesAuditOutcomes.Succeeded,
                null,
                new { process.Version },
                now
            )
        );
    }
}
