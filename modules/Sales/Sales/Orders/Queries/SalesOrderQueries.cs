using Microsoft.EntityFrameworkCore;
using ModulithFoundry.Modules.Access.Contracts;
using ModulithFoundry.Modules.Sales.Authorization;
using ModulithFoundry.Modules.Sales.Contracts;
using ModulithFoundry.Modules.Sales.Persistence;

namespace ModulithFoundry.Modules.Sales.Orders.Queries;

internal sealed class SalesOrderQueries(
    SalesDbContext context,
    SalesRequestAuthorization authorization
)
{
    internal async Task<GetSalesOrderActivityResult> GetActivityAsync(
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
        {
            return new GetSalesOrderActivityResult.PermissionDenied();
        }
        Guid? orderId = await context
            .SalesOrders.AsNoTracking()
            .Where(order =>
                order.OrganizationId == organizationId.Value && order.OrderNumber == orderNumber
            )
            .Select(order => (Guid?)order.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (orderId is null)
        {
            return new GetSalesOrderActivityResult.NotFound();
        }
        SalesOrderActivityEntry[] entries = await context
            .OrderActivity.AsNoTracking()
            .Where(entry =>
                entry.OrganizationId == organizationId.Value && entry.OrderId == orderId.Value
            )
            .OrderBy(entry => entry.OrderVersion)
            .Select(entry => new SalesOrderActivityEntry(
                entry.Kind,
                new UserId(entry.ActorUserId),
                entry.OrderVersion,
                entry.OccurredAt
            ))
            .ToArrayAsync(cancellationToken);
        return new GetSalesOrderActivityResult.Found(entries);
    }

    internal async Task<GetSalesOrderResult> GetByNumberAsync(
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
        {
            return new GetSalesOrderResult.PermissionDenied();
        }
        SalesOrderView? order = await context
            .SalesOrders.AsNoTracking()
            .Where(order =>
                order.OrganizationId == organizationId.Value && order.OrderNumber == orderNumber
            )
            .Select(SalesOrderMappings.ViewProjection)
            .SingleOrDefaultAsync(cancellationToken);
        return order is null
            ? new GetSalesOrderResult.NotFound()
            : new GetSalesOrderResult.Found(order);
    }
}
