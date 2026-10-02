using Microsoft.EntityFrameworkCore;
using ModulithFoundry.Modules.Access.Contracts;
using ModulithFoundry.Modules.Inventory.Contracts;
using ModulithFoundry.Modules.Sales.Authorization;
using ModulithFoundry.Modules.Sales.Contracts;
using ModulithFoundry.Modules.Sales.Persistence;

namespace ModulithFoundry.Modules.Sales.Fulfilment.Queries;

internal sealed class OrderFulfilmentQueries(
    SalesDbContext context,
    SalesRequestAuthorization authorization
)
{
    internal async Task<GetOrderFulfilmentResult> GetAsync(
        UserId actorUserId,
        OrganizationId organizationId,
        long orderNumber,
        CancellationToken cancellationToken
    )
    {
        if (
            !await authorization.HasPermissionAsync(
                actorUserId,
                organizationId,
                SalesPermissionIds.OrdersView,
                cancellationToken
            )
        )
            return new GetOrderFulfilmentResult.PermissionDenied();
        OrderFulfilmentView? process = await (
            from item in context.FulfilmentProcesses.AsNoTracking()
            join order in context.SalesOrders.AsNoTracking() on item.OrderId equals order.Id
            where
                item.OrganizationId == organizationId.Value
                && order.OrganizationId == organizationId.Value
                && order.OrderNumber == orderNumber
            select new OrderFulfilmentView(
                item.Id,
                order.OrderNumber,
                item.Status,
                item.CreatedAt,
                item.Version,
                item.StockingLocationId,
                item.Lines.OrderBy(line => line.LineNumber)
                    .Select(line => new OrderFulfilmentLineView(
                        line.LineNumber,
                        new StockItemId(line.StockItemId),
                        line.Quantity,
                        line.BaseUnitCode,
                        line.Status,
                        line.ReservationId,
                        line.AvailableQuantity,
                        line.ReasonCode,
                        line.AttemptCount,
                        line.ResponseDeadline,
                        line.ReplenishmentQuantity,
                        line.ReplenishmentRequirementId,
                        line.ReplenishmentRequirementNumber,
                        line.ReplenishmentReasonCode
                    ))
                    .ToArray()
            )
        ).SingleOrDefaultAsync(cancellationToken);
        return process is null
            ? new GetOrderFulfilmentResult.NotFound()
            : new GetOrderFulfilmentResult.Found(process);
    }
}
