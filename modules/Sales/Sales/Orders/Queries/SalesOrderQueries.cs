using Microsoft.EntityFrameworkCore;
using ModulithFoundry.Modules.Access.Contracts;
using ModulithFoundry.Modules.Sales.Authorization;
using ModulithFoundry.Modules.Sales.Contracts;
using ModulithFoundry.Modules.Sales.Persistence;

namespace ModulithFoundry.Modules.Sales.Orders.Queries;

internal sealed class SalesOrderQueries(SalesDbContext context, SalesRequestAuthorization authorization)
{
    internal async Task<GetSalesOrderResult> GetByNumberAsync(UserId actorUserId, OrganizationId organizationId,
        long orderNumber, CancellationToken cancellationToken)
    {
        if (!await authorization.HasPermissionAsync(actorUserId, organizationId, SalesPermissionIds.OrdersView, cancellationToken))
        {
            return new GetSalesOrderResult.PermissionDenied();
        }
        SalesOrder? order = await context.SalesOrders.AsNoTracking()
            .SingleOrDefaultAsync(order => order.OrganizationId == organizationId.Value && order.OrderNumber == orderNumber, cancellationToken);
        return order is null ? new GetSalesOrderResult.NotFound() : new GetSalesOrderResult.Found(order.ToView());
    }
}
